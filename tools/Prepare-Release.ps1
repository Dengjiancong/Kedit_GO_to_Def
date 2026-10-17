[CmdletBinding()]
param([string]$NotesFile,[switch]$Resume)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
if($Resume){& python tools/PrepareRelease.py --resume}
else {
 if(-not $NotesFile){$NotesFile=Read-Host 'Release notes file path (Markdown)'}
 & python tools/PrepareRelease.py --notes $NotesFile
}
if($LASTEXITCODE -ne 0){throw 'Release preparation stopped. No remote changes were made.'}
