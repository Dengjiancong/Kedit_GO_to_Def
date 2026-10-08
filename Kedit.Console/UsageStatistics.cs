using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Kedit.Console {
    internal sealed class UsageRow {
        public DateTime Hour {get;set;}
        public string Function {get;set;}
        public int OffsetMinutes {get;set;}
        public long Count {get;set;}
        public long Success {get;set;}
    }
    internal sealed class UsageSnapshot {
        public int Version {get;set;}
        public string Generation {get;set;}
        public DateTime Started {get;set;}
        public List<UsageRow> Rows {get;set;}
        public List<string> Warnings {get;set;}
    }
    internal sealed class UsageStore {
        public readonly string DirectoryPath;
        public UsageStore(string directory){DirectoryPath=directory;}
        public static string DefaultDirectory {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Kedit","Usage");}}
        static string NewGeneration(){return DateTime.Now.ToString("yyyyMMddHHmmss",CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString("N");}
        string GenerationPath {get{return Path.Combine(DirectoryPath,"generation.txt");}}
        public string EnsureGeneration(){
            Directory.CreateDirectory(DirectoryPath);
            if(!File.Exists(GenerationPath)) {
                try {using(var stream=new FileStream(GenerationPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read)){var data=Encoding.UTF8.GetBytes(NewGeneration());stream.Write(data,0,data.Length);stream.Flush(true);}}
                catch(IOException){if(!File.Exists(GenerationPath))throw;}
            }
            string value=File.ReadAllText(GenerationPath).Trim();DateTime start;
            if(!System.Text.RegularExpressions.Regex.IsMatch(value,@"^\d{14}_[A-Za-z0-9-]+$")||!DateTime.TryParseExact(value.Substring(0,14),"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out start))throw new InvalidDataException("统计开始标记损坏，请先导出或备份统计目录，再清空重新记录。");
            return value;
        }
        static List<UsageRow> ReadRows(string file,string generation){
            string[] lines=File.ReadAllLines(file,Encoding.UTF8);
            if(lines.Length==0||lines[0]!="KEDIT_USAGE_V1|"+generation)throw new InvalidDataException("统计文件版本或记录范围不匹配");
            var rows=new List<UsageRow>();var seen=new HashSet<string>();
            foreach(string line in lines.Skip(1)) {
                if(string.IsNullOrWhiteSpace(line))continue;
                string[] p=line.Split('|');DateTime hour;int offset;long count,success;
                if(p.Length!=5||!DateTime.TryParseExact(p[0],"yyyyMMddHH",CultureInfo.InvariantCulture,DateTimeStyles.None,out hour)||!System.Text.RegularExpressions.Regex.IsMatch(p[1],@"^[A-Za-z][A-Za-z0-9_]*$")||!int.TryParse(p[2],out offset)||Math.Abs(offset)>840||!long.TryParse(p[3],out count)||!long.TryParse(p[4],out success)||count<0||success<0||success>count||!seen.Add(p[0]+"|"+p[1]+"|"+p[2]))throw new InvalidDataException("统计行格式错误");
                rows.Add(new UsageRow{Hour=hour,Function=p[1],OffsetMinutes=offset,Count=count,Success=success});
            }
            return rows;
        }
        public UsageSnapshot Read(){
            string generation=EnsureGeneration();
            var snapshot=new UsageSnapshot{Version=1,Generation=generation,Started=DateTime.ParseExact(generation.Substring(0,14),"yyyyMMddHHmmss",CultureInfo.InvariantCulture),Rows=new List<UsageRow>(),Warnings=new List<string>()};
            foreach(string file in Directory.GetFiles(DirectoryPath,generation+"_*.tsv")) {
                try{snapshot.Rows.AddRange(ReadRows(file,generation));}
                catch(Exception ex){try{snapshot.Rows.AddRange(ReadRows(file+".bak",generation));snapshot.Warnings.Add(Path.GetFileName(file)+"：已使用上次备份，最新计数可能不完整。");}catch{snapshot.Warnings.Add(Path.GetFileName(file)+"：无法读取，"+ex.Message);}}
            }
            if(File.ReadAllText(GenerationPath).Trim()!=generation)throw new IOException("统计范围刚刚改变，请刷新。");
            // Merge per-process shards without increment-on-read, so refresh is idempotent.
            snapshot.Rows=snapshot.Rows.GroupBy(r=>new{r.Hour,r.Function,r.OffsetMinutes}).Select(g=>new UsageRow{Hour=g.Key.Hour,Function=g.Key.Function,OffsetMinutes=g.Key.OffsetMinutes,Count=g.Sum(r=>r.Count),Success=g.Sum(r=>r.Success)}).OrderBy(r=>r.Hour).ThenBy(r=>r.Function).ToList();
            return snapshot;
        }
        public void Clear(){
            Directory.CreateDirectory(DirectoryPath);
            // Publish a new generation before deletion. Running writers immediately abandon old counters.
            string next=NewGeneration();string temp=GenerationPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.WriteAllText(temp,next,new UTF8Encoding(false));
            if(File.Exists(GenerationPath))File.Replace(temp,GenerationPath,null);else File.Move(temp,GenerationPath);
            foreach(string file in Directory.GetFiles(DirectoryPath)) {
                string name=Path.GetFileName(file);
                if((name.EndsWith(".tsv")||name.EndsWith(".tsv.bak")||name.EndsWith(".tsv.tmp"))&&!name.StartsWith(next+"_",StringComparison.Ordinal))try{File.Delete(file);}catch(IOException){}
            }
        }
        public static void Export(UsageSnapshot snapshot,string file){
            if(Path.GetExtension(file).Equals(".csv",StringComparison.OrdinalIgnoreCase)) {
                var text=new StringBuilder("日期小时,功能标识,UTC偏移分钟,触发次数,已确认成功次数\r\n");
                foreach(var row in snapshot.Rows)text.AppendFormat(CultureInfo.InvariantCulture,"{0:yyyy-MM-dd HH}:00,{1},{2},{3},{4}\r\n",row.Hour,row.Function,row.OffsetMinutes,row.Count,SuccessSupported(row.Function)?row.Success.ToString(CultureInfo.InvariantCulture):"");
                File.WriteAllText(file,text.ToString(),new UTF8Encoding(true));
            } else File.WriteAllText(file,new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(snapshot),new UTF8Encoding(true));
        }
        public static bool SuccessSupported(string id){return id=="InsertFlowNode"||id=="RenumberBins"||id=="SpacesToTabs";}
        public static DateTime PeriodStart(DateTime date,int period){date=date.Date;if(period==1)return date.AddDays(-((int)date.DayOfWeek+6)%7);if(period==2)return new DateTime(date.Year,date.Month,1);if(period==3)return new DateTime(date.Year,1,1);return date;}
        public static DateTime PeriodEnd(DateTime start,int period){return period==0?start.AddDays(1):period==1?start.AddDays(7):period==2?start.AddMonths(1):start.AddYears(1);}
    }
}
