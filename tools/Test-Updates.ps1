$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dir=Join-Path $root ('.pet-test\updates-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir | Out-Null
[IO.File]::WriteAllText((Join-Path $dir 'Fixture.cs'),'class Fixture {static void Main(){System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"ran.txt"),"fixture only");}}')
$csc='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /out:"$dir\FixtureSource.exe" "$dir\Fixture.cs"
if($LASTEXITCODE -ne 0){throw 'Fixture compile failed'}
$ref='C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
& $csc /nologo /out:"$dir\TestUpdates.exe" /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.Xaml.dll "/r:$ref\PresentationCore.dll" "/r:$ref\PresentationFramework.dll" "/r:$ref\WindowsBase.dll" (Join-Path $root 'Kedit.Console\UpdateService.cs') (Join-Path $root 'Kedit.Console\UpdateInstaller.cs') (Join-Path $root 'tools\TestUpdateService.cs')
if($LASTEXITCODE -ne 0){throw 'Test compile failed'}
& "$dir\TestUpdates.exe" "$dir\fixture" "$dir\FixtureSource.exe"
if($LASTEXITCODE -ne 0){throw 'Update tests failed'}

# Exercise the real cross-bitness path query without touching installed software.
[IO.File]::WriteAllText("$dir\Owner64.cs",'class Owner64 {static void Main(){System.Threading.Thread.Sleep(30000);}}')
& $csc /nologo /platform:x64 /out:"$dir\Owner64.exe" "$dir\Owner64.cs"
if($LASTEXITCODE -ne 0){throw 'x64 owner compile failed'}
[IO.File]::WriteAllText("$dir\CrossBitness.cs",'using System;using System.Diagnostics;using System.IO;using Kedit.Console;class CrossBitness{static void Main(string[] args){if(Environment.Is64BitProcess)throw new Exception("Expected x86 checker");using(var p=Process.Start(args[0])){try{string path=UpdateInstaller.ExecutablePath(p.Id);if(!string.Equals(path,Path.GetFullPath(args[0]),StringComparison.OrdinalIgnoreCase))throw new Exception("Wrong process path");Console.WriteLine("PASS x86 updater reads x64 owner path");}finally{if(!p.HasExited)p.Kill();p.WaitForExit();}}}}')
& $csc /nologo /platform:x86 /main:CrossBitness /out:"$dir\CrossBitness.exe" /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.Xaml.dll "/r:$ref\PresentationCore.dll" "/r:$ref\PresentationFramework.dll" "/r:$ref\WindowsBase.dll" (Join-Path $root 'Kedit.Console\UpdateService.cs') (Join-Path $root 'Kedit.Console\UpdateInstaller.cs') (Join-Path $root 'tools\TestUpdateService.cs') "$dir\CrossBitness.cs"
if($LASTEXITCODE -ne 0){throw 'x86 checker compile failed'}
& "$dir\CrossBitness.exe" "$dir\Owner64.exe"
if($LASTEXITCODE -ne 0){throw 'Cross-bitness update test failed'}
