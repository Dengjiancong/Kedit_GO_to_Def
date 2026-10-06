using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using System.Web.Script.Serialization;

namespace Kedit.Console {
    internal sealed partial class PetController {
        internal PetAutomationData Automation {get;private set;}
        private DispatcherTimer automationTimer;private PetAudio audio;private int audioGeneration;
        private DateTime lastTyping=DateTime.MinValue;private string pendingWardrobe;
        private bool automationDisposed;
        internal string MusicStatus {get;private set;}
        internal string MusicBehavior {get;private set;}
        internal double MusicPeak {get;private set;}
        private DateTime musicUiUpdate;
        internal void SetMusicBehavior(string text){MusicBehavior=text;AutomationNotice();}
        private void AutomationNotice(){var h=AutomationChanged;if(h!=null)h(this,EventArgs.Empty);}
        internal event EventHandler AutomationChanged;
        private void InitializeAutomation() {
            Automation=PetAutomationData.Load();MusicStatus="未选择播放器，自动音乐陪伴已关闭。";
            automationTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
            automationTimer.Tick+=delegate{TickAutomation(DateTime.Now);};automationTimer.Start();
        }
        internal void AutomationActivity(){lastTyping=DateTime.Now;}
        internal void AutomationReady(){TickAutomation(DateTime.Now);SendWardrobe(false);StartMusic();}
        internal void SaveAutomation(){Automation.Save();AutomationNotice();}
        internal void PreviewReminder(PetReminder r) {
            PetSchedule.Validate(r);
            if(!IsReady)throw new InvalidOperationException("请先开启桌宠，等模型加载完成后再试播。");
            if(!string.IsNullOrEmpty(r.Action)&&!Resources.Any(x=>x.id==r.Action&&x.available))throw new InvalidOperationException("当前模型不支持这项动作，请改选可用动作或仅显示提示。");
            window.AutomationBubble(r.Text);window.SendAutomation(new{type="previewReminder",id=r.Action});
        }
        internal void ResumeAutomaticMusic(){if(IsReady)window.SendAutomation(new{type="resumeMusic"});StartMusic();AutomationNotice();}
        internal void SaveReminder(PetReminder r){PetSchedule.Edit(Automation,r);pendingWardrobe=null;SaveAutomation();}
        internal void DeleteReminder(string id){PetSchedule.CancelPending(Automation,id);Automation.Reminders.RemoveAll(r=>r.Id==id);pendingWardrobe=null;SaveAutomation();}
        internal void MusicOptions(string path,double amount,double sensitivity) {
            bool changed=!string.Equals(path,Automation.MusicPath,StringComparison.OrdinalIgnoreCase);
            Automation.MusicPath=path??"";Automation.MusicAmount=PetSettings.Bound(amount,0,100,70);Automation.MusicSensitivity=PetSettings.Bound(sensitivity,.5,4,1.5);
            SaveAutomation();if(changed)StartMusic();
        }
        private void StartMusic() {
            StopMusic();if(!IsReady||string.IsNullOrWhiteSpace(Automation.MusicPath))return;
            int generation=audioGeneration;var dispatcher=automationTimer.Dispatcher;
            audio=new PetAudio(Automation.MusicPath,delegate(double peak,string status){
                if(dispatcher.HasShutdownStarted)return;
                try { dispatcher.BeginInvoke(new Action(delegate {
                    if(automationDisposed||generation!=audioGeneration||!IsReady)return;
                    window.SendAutomation(new{type="music",enabled=true,peak=peak,amount=Automation.MusicAmount,sensitivity=Automation.MusicSensitivity});
                    MusicPeak=peak;MusicStatus=status;
                    if((DateTime.UtcNow-musicUiUpdate).TotalMilliseconds>=250){musicUiUpdate=DateTime.UtcNow;AutomationNotice();}
                })); } catch(InvalidOperationException) { /* Dispatcher closed while the audio thread was finishing. */ }
            });
        }
        private void StopMusic() {
            audioGeneration++;if(audio!=null){audio.Dispose();audio=null;}
            if(IsReady)window.SendAutomation(new{type="music",enabled=false,peak=0});
            MusicStatus="自动音乐陪伴已关闭；手动打碟仍可使用。";
            MusicPeak=0;
        }
        internal void TickAutomation(DateTime now) {
            try {
                AutomationNotice();
                bool changed=PetSchedule.Scan(Automation,now,IsReady);
                var w=Automation.Wardrobe;
                if(w!=null&&w.RestoreAt!=default(DateTime)&&now>=w.RestoreAt) {
                    w.values=new Dictionary<string,double>(w.original);w.RestoreAt=default(DateTime);w.Token="restored";
                    SaveAutomation();SendWardrobe(false);changed=false;
                }
                if(changed)SaveAutomation();
                if(!IsReady)return;
                // One pending reminder at a time; deterministic ordering for coincident appointments.
                foreach(var o in Automation.Occurrences.Where(o=>(o.Status=="pending"||o.Status=="snoozed")&&o.Next<=now).OrderBy(o=>o.Due).ThenBy(o=>o.Key).ToArray()) {
                    var r=Automation.Reminders.First(x=>x.Id==o.ReminderId);
                    if(!o.BubbleShown){o.BubbleShown=true;SaveAutomation();window.AutomationBubble(r.Text);}
                    if(!r.Exact&&(now-lastTyping).TotalSeconds<2)continue;
                    // Give the visible reminder three seconds for snooze/ignore before starting the action.
                    if(!r.Exact&&(now-o.Next).TotalSeconds<3)continue;
                    o.Status="executed";SaveAutomation();
                    if(r.Action=="motion-cloth off") {
                        pendingWardrobe=o.Key;window.SendAutomation(new{type="wardrobePrepare",token=o.Key,action=r.Action});
                    } else if(!string.IsNullOrEmpty(r.Action))window.CompanionCommand("select",r.Action,false);
                    break;
                }
            }catch(Exception e){SetCompanionStatus("自动陪伴暂未执行："+e.Message);}
        }
        internal void AutomationMessage(Dictionary<string,object> data) {
            string type=Convert.ToString(data["type"]);
            if(type=="wardrobeManual") {pendingWardrobe=null;Automation.Wardrobe=null;SaveAutomation();return;}
            string token=Convert.ToString(data["token"]);if(token!=pendingWardrobe)return;
            var o=Automation.Occurrences.FirstOrDefault(x=>x.Key==token&&x.Status=="executed");
            var r=o==null?null:Automation.Reminders.FirstOrDefault(x=>x.Id==o.ReminderId&&x.Enabled);
            if(r==null)return;
            var json=new JavaScriptSerializer();
            var values=json.Deserialize<Dictionary<string,double>>(json.Serialize(data["values"]));
            var original=json.Deserialize<Dictionary<string,double>>(json.Serialize(data["original"]));
            if(values.Count==0)return;
            Automation.Wardrobe=new PetWardrobe{Token=token,Model=Settings.ModelPath,values=values,original=original,
                RestoreAt=r.Policy=="once"?DateTime.Now.AddSeconds(6):r.Policy=="morning"?o.RestoreAt:default(DateTime)};
            pendingWardrobe=null;SaveAutomation();SendWardrobe(true);
        }
        private void SendWardrobe(bool play) {
            if(!IsReady)return;
            var w=Automation.Wardrobe;
            window.SendAutomation(new{type="wardrobe",state=w!=null&&string.Equals(w.Model,Settings.ModelPath,StringComparison.OrdinalIgnoreCase)?w:null,play=play});
        }
        internal PetOccurrence LatestOccurrence(string reminderId) {
            return Automation.Occurrences.Where(o=>o.ReminderId==reminderId&&o.Due.Date==DateTime.Now.Date).OrderByDescending(o=>o.Due).FirstOrDefault();
        }
        internal void RespondReminder(string id,bool snooze) {
            var o=LatestOccurrence(id);var r=Automation.Reminders.FirstOrDefault(x=>x.Id==id);if(r==null)return;
            if(o==null&&!snooze&&PetSchedule.ValidTime(r.Time)) {
                DateTime due=DateTime.Today.Add(TimeSpan.Parse(r.Time));
                if((r.Days&(1<<(int)DateTime.Today.DayOfWeek))==0)return;
                o=new PetOccurrence{Key=r.Id+"/"+due.ToString("yyyyMMddHHmm"),ReminderId=id,Due=due,Status="ignored"};
                if(!Automation.Occurrences.Any(x=>x.Key==o.Key))Automation.Occurrences.Add(o);
            }
            if(o==null)return;
            pendingWardrobe=null;
            if(snooze)PetSchedule.Snooze(o,r,DateTime.Now);else o.Status="ignored";
            // An already-applied appearance keeps its existing restoration deadline.
            SaveAutomation();
        }
        internal void RestoreWardrobe() {
            var w=Automation.Wardrobe;if(w==null)return;
            w.values=new Dictionary<string,double>(w.original);w.Token="restored";w.RestoreAt=default(DateTime);
            pendingWardrobe=null;SaveAutomation();SendWardrobe(false);
        }
        private void DisposeAutomation(){automationDisposed=true;automationTimer.Stop();StopMusic();}
    }
}
