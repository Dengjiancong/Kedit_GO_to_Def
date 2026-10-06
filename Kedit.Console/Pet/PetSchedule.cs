using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Kedit.Console {
    public sealed class PetReminder {
        public string Id {get;set;} public string Name {get;set;} public bool Enabled {get;set;}
        public string Time {get;set;} public int Days {get;set;} public string Action {get;set;}
        public string Text {get;set;} public string Policy {get;set;} public string RestoreTime {get;set;}
        public bool Exact {get;set;} public int CatchUpMinutes {get;set;} public int SnoozeMinutes {get;set;}
        public PetReminder() { Id=Guid.NewGuid().ToString("N");Name="下班提醒";Time="18:00";Days=62;Action="motion-cloth off";Text="下班啦！下班啦！辛苦啦！";Policy="morning";RestoreTime="07:00";SnoozeMinutes=10; }
        public string Display {get{return (Enabled?"● ":"○ ")+Name+" · "+Time;}}
    }
    public sealed class PetOccurrence {
        public string Key {get;set;} public string ReminderId {get;set;} public DateTime Due {get;set;}
        public DateTime Next {get;set;} public DateTime Deadline {get;set;} public DateTime RestoreAt {get;set;}
        public string Status {get;set;} public bool BubbleShown {get;set;}
    }
    public sealed class PetWardrobe {
        public string Token {get;set;} public string Model {get;set;} public DateTime RestoreAt {get;set;}
        public Dictionary<string,double> values {get;set;} public Dictionary<string,double> original {get;set;}
    }
    public sealed class PetAutomationData {
        public List<PetReminder> Reminders {get;set;} public List<PetOccurrence> Occurrences {get;set;}
        public PetWardrobe Wardrobe {get;set;} public DateTime LastCheck {get;set;}
        public string MusicPath {get;set;} public double MusicAmount {get;set;} public double MusicSensitivity {get;set;}
        public PetAutomationData() {Reminders=new List<PetReminder>();Occurrences=new List<PetOccurrence>();MusicPath="";MusicAmount=70;MusicSensitivity=1.5;}
        public static PetAutomationData Load() {
            try {
                string p=Path.Combine(PetRuntime.DataDirectory,"automation.json");
                var d=File.Exists(p)?new JavaScriptSerializer().Deserialize<PetAutomationData>(File.ReadAllText(p,Encoding.UTF8)):new PetAutomationData();
                if(d==null)return new PetAutomationData();
                d.Reminders=(d.Reminders??new List<PetReminder>()).Where(r=>r!=null&&!string.IsNullOrEmpty(r.Id)).ToList();
                d.Occurrences=d.Occurrences??new List<PetOccurrence>();
                d.LastCheck=Local(d.LastCheck);
                foreach(var o in d.Occurrences){o.Due=Local(o.Due);o.Next=Local(o.Next);o.Deadline=Local(o.Deadline);o.RestoreAt=Local(o.RestoreAt);}
                if(d.Wardrobe!=null)d.Wardrobe.RestoreAt=Local(d.Wardrobe.RestoreAt);
                d.MusicAmount=PetSettings.Bound(d.MusicAmount,0,100,70);d.MusicSensitivity=PetSettings.Bound(d.MusicSensitivity,.5,4,1.5);
                return d;
            }catch(Exception e){PetRuntime.Log("Automation load: "+e.Message);return new PetAutomationData();}
        }
        private static DateTime Local(DateTime value){return value.Year==1?default(DateTime):value.Kind==DateTimeKind.Utc?value.ToLocalTime():value;}
        public void Save() {
            Directory.CreateDirectory(PetRuntime.DataDirectory);
            string p=Path.Combine(PetRuntime.DataDirectory,"automation.json");
            File.WriteAllText(p+".tmp",new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));
            if(File.Exists(p))File.Replace(p+".tmp",p,null);else File.Move(p+".tmp",p);
        }
    }
    // Local wall-clock appointments. Persist decisions before issuing visual effects.
    public static class PetSchedule {
        public static DateTime? Next(PetAutomationData d,PetReminder r,DateTime now) {
            if(r==null||!r.Enabled||!ValidTime(r.Time))return null;
            var pending=d.Occurrences.Where(o=>o.ReminderId==r.Id&&(o.Status=="pending"||o.Status=="snoozed")&&o.Deadline>=now).OrderBy(o=>o.Next).FirstOrDefault();
            if(pending!=null)return pending.Next;
            for(int day=0;day<=7;day++) {
                DateTime date=now.Date.AddDays(day),due=date.Add(TimeSpan.Parse(r.Time));
                if(due<now||(r.Days&(1<<(int)date.DayOfWeek))==0)continue;
                string key=r.Id+"/"+due.ToString("yyyyMMddHHmm");
                if(!d.Occurrences.Any(o=>o.Key==key))return due;
            }
            return null;
        }
        public static bool ValidTime(string text) {DateTime t;return DateTime.TryParseExact(text,"HH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out t);}
        public static void Validate(PetReminder r) {
            if(!ValidTime(r.Time)||!ValidTime(r.RestoreTime))throw new ArgumentException("时间请使用 HH:mm，例如 18:00、07:00。");
            if((r.Days&127)==0)throw new ArgumentException("请至少选择一天。");
            if(string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.Text))throw new ArgumentException("请填写名称和提示语。");
            if(r.Policy!="once"&&r.Policy!="morning"&&r.Policy!="hold")throw new ArgumentException("请选择动作结束策略。");
            r.CatchUpMinutes=Math.Max(0,Math.Min(60,r.CatchUpMinutes));r.SnoozeMinutes=Math.Max(1,Math.Min(120,r.SnoozeMinutes));
        }
        public static bool Scan(PetAutomationData d,DateTime now,bool ready) {
            bool changed=false;
            // Today and yesterday cover the configurable (<= 60 minute) catch-up across midnight.
            foreach(var r in d.Reminders.Where(item=>item.Enabled&&ValidTime(item.Time)&&ValidTime(item.RestoreTime)))for(int day=-1;day<=0;day++) {
                DateTime date=now.Date.AddDays(day),due=date.Add(TimeSpan.Parse(r.Time));
                if((r.Days&(1<<(int)date.DayOfWeek))==0||due>now)continue;
                string key=r.Id+"/"+due.ToString("yyyyMMddHHmm");
                if(d.Occurrences.Any(o=>o.Key==key))continue;
                bool crossed=d.LastCheck!=default(DateTime)&&d.LastCheck<due&&(now-d.LastCheck).TotalSeconds<=5;
                bool eligible=ready&&(crossed||(r.CatchUpMinutes>0&&(now-due).TotalMinutes<=r.CatchUpMinutes));
                d.Occurrences.Add(new PetOccurrence {Key=key,ReminderId=r.Id,Due=due,Next=eligible?now:due,
                    Deadline=due.AddMinutes(Math.Max(1,r.CatchUpMinutes)),RestoreAt=date.AddDays(1).Add(TimeSpan.Parse(r.RestoreTime)),Status=eligible?"pending":"missed"});
                changed=true;
            }
            foreach(var o in d.Occurrences.Where(o=>o.Status=="pending"||o.Status=="snoozed")) {
                var r=d.Reminders.FirstOrDefault(item=>item.Id==o.ReminderId&&item.Enabled);
                if(r==null||now>o.Deadline||(r.Policy=="morning"&&now>=o.RestoreAt)){o.Status="expired";changed=true;}
            }
            d.LastCheck=now;return changed;
        }
        public static void Edit(PetAutomationData d,PetReminder r) {
            Validate(r);d.Reminders.RemoveAll(x=>x.Id==r.Id);d.Reminders.Add(r);
            CancelPending(d,r.Id);
        }
        public static void CancelPending(PetAutomationData d,string id) {
            foreach(var o in d.Occurrences.Where(o=>o.ReminderId==id&&(o.Status=="pending"||o.Status=="snoozed")))o.Status="cancelled";
        }
        public static void Snooze(PetOccurrence o,PetReminder r,DateTime now) {
            o.Status="snoozed";o.Next=now.AddMinutes(r.SnoozeMinutes);o.Deadline=o.Next.AddMinutes(Math.Max(1,r.CatchUpMinutes));o.BubbleShown=false;
        }
    }
}
