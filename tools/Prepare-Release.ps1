[CmdletBinding()]
param([string]$NotesFile,[switch]$Resume)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
# A failed preparation already contains its notes and allocated version.
$pendingPath='Release_exe/.prepared-release.json'
if(-not $Resume -and (Test-Path -LiteralPath $pendingPath)){
 $pending=Get-Content -LiteralPath $pendingPath -Raw -Encoding UTF8 | ConvertFrom-Json
 if($pending.status -eq 'building'){$Resume=$true}
}
if($Resume){& python tools/PrepareRelease.py --resume}
else {
 if(-not $NotesFile){$NotesFile=Read-Host 'Release notes file path (Markdown)'}
 & python tools/PrepareRelease.py --notes $NotesFile
}
if($LASTEXITCODE -ne 0){throw 'Release preparation stopped. No remote changes were made.'}
