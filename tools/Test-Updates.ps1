$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dir=Join-Path $root ('.pet-test\updates-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir | Out-Null
[IO.File]::WriteAllText((Join-Path $dir 'Fixture.cs'),'class Fixture {static void Main(){System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"ran.txt"),"fixture only");}}')
$csc='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /out:"$dir\FixtureSource.exe" "$dir\Fixture.cs"
if($LASTEXITCODE -ne 0){throw 'Fixture compile failed'}
[IO.File]::WriteAllText("$dir\HealthFixture.cs",'using System;using System.IO;using System.Threading;class HealthFixture{static void Main(string[] args){string d=AppDomain.CurrentDomain.BaseDirectory;File.WriteAllText(Path.Combine(d,"pid.txt"),System.Diagnostics.Process.GetCurrentProcess().Id.ToString());string mode=File.ReadAllText(Path.Combine(d,"mode.txt"));if(mode=="exit")return;if(mode!="timeout"){using(var e=EventWaitHandle.OpenExisting(args[1]))e.Set();if(mode=="early-exit")return;}Thread.Sleep(30000);}}')
& $csc /nologo /out:"$dir\HealthFixture.exe" "$dir\HealthFixture.cs"
if($LASTEXITCODE -ne 0){throw "Health fixture compile failed"}
$ref='C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
& $csc /nologo /out:"$dir\TestUpdates.exe" /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.Xaml.dll "/r:$ref\PresentationCore.dll" "/r:$ref\PresentationFramework.dll" "/r:$ref\WindowsBase.dll" (Join-Path $root 'Kedit.Console\UpdateService.cs') (Join-Path $root 'Kedit.Console\UpdateInstaller.cs') (Join-Path $root 'tools\TestUpdateService.cs')
if($LASTEXITCODE -ne 0){throw 'Test compile failed'}
& "$dir\TestUpdates.exe" "$dir\fixture" "$dir\FixtureSource.exe" "$dir\HealthFixture.exe"
if($LASTEXITCODE -ne 0){throw 'Update tests failed'}

# Exercise the real cross-bitness path query without touching installed software.
[IO.File]::WriteAllText("$dir\Owner64.cs",'class Owner64 {static void Main(){System.Threading.Thread.Sleep(30000);}}')
& $csc /nologo /platform:x64 /out:"$dir\Owner64.exe" "$dir\Owner64.cs"
if($LASTEXITCODE -ne 0){throw 'x64 owner compile failed'}
[IO.File]::WriteAllText("$dir\CrossBitness.cs",'using System;using System.Diagnostics;using System.IO;using Kedit.Console;class CrossBitness{static void Main(string[] args){if(Environment.Is64BitProcess)throw new Exception("Expected x86 checker");using(var p=Process.Start(args[0])){try{string path=UpdateInstaller.ExecutablePath(p.Id);if(!string.Equals(path,Path.GetFullPath(args[0]),StringComparison.OrdinalIgnoreCase))throw new Exception("Wrong process path");using(var service=new UpdateService()){service.Configure("v19.00-Amiya.v101",path,true);typeof(UpdateService).GetField("package",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(service,path);string manifest=UpdateInstaller.Prepare(service,p);var plan=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<UpdateInstaller.Plan>(File.ReadAllText(manifest));try{if(plan.Owner!=p.Id||plan.OwnerStart!=p.StartTime.ToUniversalTime().Ticks||plan.Target!=path||!File.Exists(plan.Staged)||!File.Exists(Path.Combine(Path.GetDirectoryName(manifest),"Kedit.Updater.exe")))throw new Exception("Incomplete install plan");UpdateService.VerifyPackage(plan.Staged,new FileInfo(path).Length,plan.Sha,System.Threading.CancellationToken.None);}finally{File.Delete(plan.Staged);}service.TargetPath=typeof(CrossBitness).Assembly.Location;bool rejected=false;try{UpdateInstaller.Prepare(service,p);}catch(IOException){rejected=true;}if(!rejected)throw new Exception("Wrong target accepted");}Console.WriteLine("PASS x86 updater: x64 path, full Prepare, staging hash, owner identity, wrong target rejection");}finally{if(!p.HasExited)p.Kill();p.WaitForExit();}}}}')
& $csc /nologo /platform:x86 /main:CrossBitness /out:"$dir\CrossBitness.exe" /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.Xaml.dll "/r:$ref\PresentationCore.dll" "/r:$ref\PresentationFramework.dll" "/r:$ref\WindowsBase.dll" (Join-Path $root 'Kedit.Console\UpdateService.cs') (Join-Path $root 'Kedit.Console\UpdateInstaller.cs') (Join-Path $root 'tools\TestUpdateService.cs') "$dir\CrossBitness.cs"
if($LASTEXITCODE -ne 0){throw 'x86 checker compile failed'}
& "$dir\CrossBitness.exe" "$dir\Owner64.exe"
if($LASTEXITCODE -ne 0){throw 'Cross-bitness update test failed'}
