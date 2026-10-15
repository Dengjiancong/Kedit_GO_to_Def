$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dir=Join-Path $root '.pet-test\column-rectangle'
New-Item -ItemType Directory -Force $dir | Out-Null
$ref='C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /out:"$dir\Test.exe" /r:System.Xaml.dll "/r:$ref\PresentationCore.dll" "/r:$ref\PresentationFramework.dll" "/r:$ref\WindowsBase.dll" "$root\Kedit.Console\ColumnFillWindow.cs" "$root\tools\TestColumnFill.cs"
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
& "$dir\Test.exe" $dir
if($LASTEXITCODE -ne 0){throw 'Rectangle tests failed'}
