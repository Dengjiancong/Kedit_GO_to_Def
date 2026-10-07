$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root 'Kedit.Console\bin\PreviewDrag\Kedit.Console.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Please build Kedit.Console Release first.' }
Start-Process -FilePath $exe -ArgumentList '--design-preview' -WorkingDirectory $root
