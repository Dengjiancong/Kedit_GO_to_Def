param([switch]$Audio)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dir=Join-Path $root '.pet-test\automation-unit'
New-Item -ItemType Directory -Path $dir -Force|Out-Null
$files=@('PetRuntime.cs','PetSettings.cs','PetSchedule.cs','PetAudio.cs')|ForEach-Object {Join-Path $root ('Kedit.Console\Pet\'+$_)}
$test=@'
using System;
using System.IO;
using System.Linq;
using System.Threading;
namespace Kedit.Console {
public static class AutomationChecks {
static void Check(bool b,string s){if(!b)throw new Exception(s);}
public static string Run(string directory,bool audio) {
 PetRuntime.Configure(directory);
 DateTime due=new DateTime(2026,10,9,18,0,0);
 var d=new PetAutomationData();var r=new PetReminder{Enabled=true,Days=32,Exact=true};d.Reminders.Add(r);d.LastCheck=due.AddSeconds(-1);
 Check(PetSchedule.Scan(d,due,true),"due appointment");Check(d.Occurrences.Count==1,"only selected weekdays recorded");
 var o=d.Occurrences.Single(x=>x.Due==due);Check(o.Status=="pending","on-time pending");Check(o.RestoreAt==due.Date.AddDays(1).AddHours(7),"Friday restores Saturday");
 o.Status="executed";d.Save();d=PetAutomationData.Load();PetSchedule.Scan(d,due.AddSeconds(1),true);Check(d.Occurrences.Count(x=>x.Due==due)==1,"restart deduplication");
 PetSchedule.Scan(d,due.AddHours(-1),true);PetSchedule.Scan(d,due,true);Check(d.Occurrences.Single(x=>x.Due==due).Status=="executed","clock rollback");
 var missed=new PetAutomationData();missed.Reminders.Add(r);PetSchedule.Scan(missed,due.AddMinutes(3),true);Check(missed.Occurrences.Single(x=>x.Due==due).Status=="missed","no catch-up by default");
 r.CatchUpMinutes=5;var caught=new PetAutomationData();caught.Reminders.Add(r);PetSchedule.Scan(caught,due.AddMinutes(3),true);Check(caught.Occurrences.Single(x=>x.Due==due).Status=="pending","bounded catch-up");
 o=caught.Occurrences.Single(x=>x.Due==due);PetSchedule.Snooze(o,r,due.AddMinutes(3));Check(o.Next==due.AddMinutes(13),"snooze original occurrence");
 PetSchedule.Scan(caught,o.Next,true);Check(o.Status=="snoozed","snooze remains valid");PetSchedule.Edit(caught,r);Check(o.Status=="cancelled","edit cancels pending");
 var late=new PetOccurrence{ReminderId=r.Id,Status="snoozed",Due=due,Next=due.AddDays(1),Deadline=due.AddDays(2),RestoreAt=due.Date.AddDays(1).AddHours(7)};caught.Occurrences.Add(late);PetSchedule.Scan(caught,late.RestoreAt,true);Check(late.Status=="expired","no undress after restoration deadline");
 Check(!PetSchedule.ValidTime("25:00")&&PetSchedule.ValidTime("07:00"),"time validation");
 foreach(int mask in new[]{127,62,32,5}) {var weekly=new PetAutomationData();weekly.Reminders.Add(new PetReminder{Enabled=true,Days=mask,CatchUpMinutes=5});for(int i=0;i<7;i++){var t=due.Date.AddDays(i).AddHours(18);PetSchedule.Scan(weekly,t,true);Check(weekly.Occurrences.Any(x=>x.Due==t)==((mask&(1<<(int)t.DayOfWeek))!=0),"repeat mask");}}
 if(audio) {
  string wave=Path.Combine(directory,"meter-test.wav");using(var b=new BinaryWriter(File.Create(wave))) {int samples=22050*2;b.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));b.Write(36+samples*2);b.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));b.Write(16);b.Write((short)1);b.Write((short)1);b.Write(22050);b.Write(44100);b.Write((short)2);b.Write((short)16);b.Write(System.Text.Encoding.ASCII.GetBytes("data"));b.Write(samples*2);for(int i=0;i<samples;i++)b.Write((short)(1200*Math.Sin(i*2*Math.PI*330/22050)));}
  double max=0,other=0;string status="";using(var player=new System.Media.SoundPlayer(wave))using(var meter=new PetAudio(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,(p,s)=>{max=Math.Max(max,p);status=s;}))using(var wrong=new PetAudio("Z:\\not-selected.exe",(p,s)=>other=Math.Max(other,p))) {player.PlayLooping();Thread.Sleep(4500);player.Stop();Thread.Sleep(300);}
  Check(max>.001,"selected audio meter: "+status);Check(other==0,"unselected process leaked audio");
 }
 return "PASS: weekly masks, default missed policy, bounded catch-up, persistent deduplication/clock rollback, snooze, edit cancellation, cross-night deadline"+(audio?", real selected-process peak meter and unrelated-process exclusion":"");
}
}}
'@
$check=Join-Path $dir 'checks.cs'
[IO.File]::WriteAllText($check,$test,[Text.UTF8Encoding]::new($false))
Add-Type -Path ($files+@($check)) -ReferencedAssemblies System.dll,System.Core.dll,System.Web.Extensions.dll
[Kedit.Console.AutomationChecks]::Run($dir,[bool]$Audio)
