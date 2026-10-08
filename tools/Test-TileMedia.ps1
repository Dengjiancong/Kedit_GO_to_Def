$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dir=Join-Path $root ('.pet-test\tile-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir | Out-Null
[IO.File]::WriteAllBytes((Join-Path $dir 'sample.gif'),[Convert]::FromBase64String('R0lGODlhAgABAIEAAAAAAP8AAAAA/////yH5BAkIAAAALAAAAAACAAEAAAICDFEAIfkECRAAAAAsAAAAAAIAAQAAAgIEVQA7'))
$wpf='C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
$exe=Join-Path $dir 'check.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /out:$exe /r:System.Drawing.dll /r:System.Xaml.dll "/r:$wpf\WindowsBase.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\PresentationFramework.dll" (Join-Path $root 'Kedit.Console\TileMedia.cs') (Join-Path $root 'Kedit.Console\PreviewCrop.cs') (Join-Path $root 'tools\TestTileMedia.cs')
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
& $exe (Join-Path $dir 'sample.gif') (Join-Path $root 'Kedit.Console\bin\Release\Kedit.Console.exe')
if($LASTEXITCODE -ne 0){throw 'Tile tests failed'}
