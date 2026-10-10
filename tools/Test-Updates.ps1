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
