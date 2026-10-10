using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Reflection;
using System.Security.Cryptography;
namespace Kedit.Console {
    internal sealed class PageMedia {
        readonly string project;
        readonly Dictionary<string,string> map;
        readonly Assembly assembly=typeof(PageMedia).Assembly;
        public PageMedia(string root){
            project=File.Exists(Path.Combine(root,"Kedit.Console","Kedit.Console.csproj"))?root:null;
            string json="{}";
            if(project!=null&&File.Exists(Manifest))json=File.ReadAllText(Manifest);
            else using(var input=assembly.GetManifestResourceStream("PageMedia/pages.json"))if(input!=null)using(var reader=new StreamReader(input))json=reader.ReadToEnd();
            map=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(json)??new Dictionary<string,string>();
        }
        string Manifest {get{return Path.Combine(project,"mp4","pages.json");}}
        public string ProjectDirectory {get{return project;}}
        public string Describe(string page){string file;return "素材项目："+(project??"未设置（使用 EXE 内置素材）")+"\n当前页面："+page+" → "+(map.TryGetValue(page,out file)?file:"默认素材")+(project==null?"":"\n保存清单："+Manifest);}
        public bool Has(string page){return map.ContainsKey(page);}
        public string Resolve(string page,string fallback){
            string file;if(!map.TryGetValue(page,out file))return fallback;
            if(Path.GetFileName(file)!=file)throw new InvalidDataException("素材路径必须是 mp4 目录内的文件名。");
            if(project!=null){string source=Path.Combine(project,"mp4",file);if(!File.Exists(source))throw new FileNotFoundException("页面 "+page+" 的素材不存在："+file);return source;}
            string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Kedit","PageMedia",assembly.ManifestModule.ModuleVersionId.ToString());Directory.CreateDirectory(cache);string path=Path.Combine(cache,file);
            if(!File.Exists(path))using(var input=assembly.GetManifestResourceStream("PageMedia/"+file)){if(input==null)throw new FileNotFoundException("打包素材缺失："+file);using(var output=File.Create(path))input.CopyTo(output);}
            return path;
        }
        public void Set(string page,string source){
            RequireProject();string extension=Path.GetExtension(source).ToLowerInvariant();if(extension!=".mp4"&&extension!=".gif"&&extension!=".png"&&extension!=".jpg"&&extension!=".jpeg")throw new InvalidDataException("支持 MP4 / GIF / PNG / JPG / JPEG；WebP 请通过素材选择器导入转换。");
            string directory=Path.Combine(project,"mp4");Directory.CreateDirectory(directory);string full=Path.GetFullPath(source);string file=Path.GetFileName(full);
            if(!string.Equals(Path.GetDirectoryName(full),directory,StringComparison.OrdinalIgnoreCase)){
                using(var hash=SHA256.Create())using(var input=File.OpenRead(full))file=Path.GetFileNameWithoutExtension(full)+"-"+BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").Substring(0,12)+extension;
                string dest=Path.Combine(directory,file);if(!File.Exists(dest))File.Copy(full,dest);
            }
            string old;bool existed=map.TryGetValue(page,out old);map[page]=file;try{Save();}catch{if(existed)map[page]=old;else map.Remove(page);throw;}
        }
        public sealed class Framing {
            public double Zoom {get;set;} public double X {get;set;} public double Y {get;set;}
            public Framing(){Zoom=1;X=.5;Y=.5;}
            public void Validate(){if(double.IsNaN(Zoom)||double.IsNaN(X)||double.IsNaN(Y)||Zoom<1||Zoom>4||X<0||X>1||Y<0||Y>1)throw new InvalidDataException("背景构图参数无效。");}
        }
        Dictionary<string,Framing> ReadFraming(){string json="{}";if(project!=null&&File.Exists(Path.Combine(project,"mp4","framing.json")))json=File.ReadAllText(Path.Combine(project,"mp4","framing.json"));else using(var stream=assembly.GetManifestResourceStream("PageMedia/framing.json"))if(stream!=null)using(var reader=new StreamReader(stream))json=reader.ReadToEnd();var result=new JavaScriptSerializer().Deserialize<Dictionary<string,Framing>>(json);foreach(var item in result.Values)item.Validate();return result;}
        public bool HasFraming(string page){return ReadFraming().ContainsKey(page);}
        public Framing GetFraming(string page){Framing value;return ReadFraming().TryGetValue(page,out value)?value:new Framing();}
        public void SetFraming(string page,Framing value){RequireProject();value.Validate();var map=ReadFraming();map[page]=value;string path=Path.Combine(project,"mp4","framing.json");Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(map));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
        void RequireProject(){if(project==null)throw new InvalidOperationException("请先点击管理员区的“素材项目目录”，选择包含 Kedit.Console 的项目根目录。");}
        void Save(){string temporary=Manifest+".tmp";File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(map));if(File.Exists(Manifest))File.Replace(temporary,Manifest,null);else File.Move(temporary,Manifest);}
        public void Reset(string page){RequireProject();string old;if(!map.TryGetValue(page,out old))return;map.Remove(page);try{Save();}catch{map[page]=old;throw;}}
        public string Listing(){var lines=new List<string>();string[] pages={"home","usage","kedit.FindClipboard","kedit.GoToDef","kedit.ShiftF2","kedit.AltF","kedit.CtrlW","kedit.AltA","kedit.ColumnInsert","kedit.ToggleComment","kedit.SpacesToTabs","kedit.SmartClick","kedit.RenumberBins","kedit.InsertFlowNode","vs.VS_Peek","vs.VS_Back","vs.VS_Build","vs.VS_ToggleComment","vs.VS_BookmarkToggle","vs.VS_BookmarkNext","vs.VS_BookmarkPrevious","vs.VS_Redo","vs.VS_BookmarkClear","pet.display","pet.follow","pet.typing","pet.props","pet.care","pet.music","pet.schedule","pet.osd","updates","updates.connecting","updates.connection-failed","updates.latest","updates.available","updates.downloading","updates.verifying","updates.ready","updates.installing","updates.download-failed","updates.cancelled","other"};foreach(string page in pages){string file;lines.Add(page+" → "+(map.TryGetValue(page,out file)?file:"未设置（使用默认素材）"));}return string.Join("\n",lines.ToArray());}
    }
}
