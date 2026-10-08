$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dir=Join-Path $root ('.pet-test\usage-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir | Out-Null
$source=[IO.File]::ReadAllText((Join-Path $root 'Kedit_GO_to_Def_ver18.30.ahk'))
$functions=$source.Substring($source.IndexOf('UsageInitialize() {'))
$functions=$functions.Replace('A_AppData . "\Kedit\Usage"','A_ScriptDir . "\store"')
$functions=$functions.Substring(0,$functions.IndexOf('Label_UsagePause:'))
$harness=@'
#NoEnv
#SingleInstance Off
UsageInitialize()
Loop, 1000
    UsageHit("FindClipboard")
Token := UsageHit("InsertFlowNode")
UsageHit("InsertFlowNode", true, Token)
UsageFlush()
FileRead, Before, %UsageDirectory%\%UsageGeneration%_%UsageFile%.tsv
if (!InStr(Before, "|FindClipboard|") || !InStr(Before, "|1000|0") || !InStr(Before, "|1|1"))
    ExitApp, 1
FileDelete, %UsageDirectory%\generation.txt
FileAppend, 20261008000000_testclear, %UsageDirectory%\generation.txt, UTF-8-RAW
UsageHit("InsertFlowNode", true, Token)
UsageHit("FindClipboard")
UsageFlush()
FileRead, After, %UsageDirectory%\%UsageGeneration%_%UsageFile%.tsv
if (InStr(After, "InsertFlowNode") || !InStr(After, "|1|0"))
    ExitApp, 2
UsageHit("FindClipboard")
OnExit("UsageExit")
ExitApp, 0
'@
$path=Join-Path $dir 'writer.ahk'
[IO.File]::WriteAllText($path,$harness+"`r`n"+$functions,[Text.UTF8Encoding]::new($true))
$p=Start-Process 'C:\Program Files\AutoHotkey\AutoHotkey.exe' -ArgumentList @('/ErrorStdOut',('"'+$path+'"')) -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $dir 'writer.log')
if($p.ExitCode -ne 0){throw ('AHK usage check failed '+$p.ExitCode+' '+[IO.File]::ReadAllText((Join-Path $dir 'writer.log')))}
$test=@'
using System;
using System.IO;
using System.Linq;
using Kedit.Console;
class CheckUsage {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(string[] args){
  var store=new UsageStore(args[0]);var data=store.Read();
  Check(data.Rows.Count==1&&data.Rows[0].Count==2,"AHK exit flush / clear epoch");
  Check(store.Read().Rows.Sum(r=>r.Count)==2,"repeat read must not accumulate");
  string prefix="KEDIT_USAGE_V1|"+data.Generation+"\n";
  string file=Path.Combine(args[0],data.Generation+"_fixture.tsv");
  File.WriteAllText(file,prefix+"2024022923|RenumberBins|480|5|3\n2024030100|RenumberBins|480|7|5\n2025123123|VS_BookmarkNext|480|4|0\n2026010100|VS_BookmarkNext|480|6|0\n");
  data=store.Read();Check(data.Rows.Sum(r=>r.Count)==24,"multi session aggregate");
  Check(UsageStore.PeriodStart(new DateTime(2026,10,11),1)==new DateTime(2026,10,5),"Monday week start");
  Check(UsageStore.PeriodEnd(new DateTime(2024,2,1),2)==new DateTime(2024,3,1),"leap month end");
  Check(data.Rows.Where(r=>r.Hour>=new DateTime(2024,2,1)&&r.Hour<new DateTime(2024,3,1)).Sum(r=>r.Count)==5,"month boundary");
  Check(data.Rows.Where(r=>r.Hour.Year==2025).Sum(r=>r.Count)==4,"year boundary");
  UsageStore.Export(data,Path.Combine(args[0],"export.csv"));UsageStore.Export(data,Path.Combine(args[0],"export.json"));
  Check(File.ReadAllText(Path.Combine(args[0],"export.csv")).Contains("RenumberBins,480,5,3"),"CSV export");
  Check(File.ReadAllText(Path.Combine(args[0],"export.json")).Contains("Generation"),"JSON metadata");
  File.Copy(file,file+".bak");File.WriteAllText(file,"broken");
  data=store.Read();Check(data.Rows.Sum(r=>r.Count)==24&&data.Warnings.Count==1,"backup recovery");
  File.Delete(file+".bak");Check(store.Read().Warnings.Count==1,"corruption warning");
  store.Clear();data=store.Read();Check(data.Rows.Count==0&&data.Generation!="20261008000000_testclear","clear starts new epoch");
  Console.WriteLine("PASS: AHK aggregation / exit flush / clear isolation; multi-session read / periods / export / corrupt recovery");
 }
}
'@
$cs=Join-Path $dir 'CheckUsage.cs';[IO.File]::WriteAllText($cs,$test)
$exe=Join-Path $dir 'CheckUsage.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /r:System.Core.dll /r:System.Web.Extensions.dll /out:$exe $cs (Join-Path $root 'Kedit.Console\UsageStatistics.cs')
if($LASTEXITCODE -ne 0){throw 'Usage test compilation failed'}
& $exe (Join-Path $dir 'store')
if($LASTEXITCODE -ne 0){throw 'Usage store tests failed'}
