[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Version,[Parameter(Mandatory=$true)][string]$Exe,[Parameter(Mandatory=$true)][string]$Source,[Parameter(Mandatory=$true)][string]$NotesFile,[switch]$Apply,[switch]$Publish)
$arguments=@((Join-Path $PSScriptRoot 'RepositoryMirror.py'),'publish','--version',$Version,'--exe',$Exe,'--source',$Source,'--notes',$NotesFile)
if($Apply){$arguments+='--apply'}
if($Publish){$arguments+='--publish'}
& python @arguments
if($LASTEXITCODE -ne 0){throw 'Release publishing stopped; existing remote assets were not deleted.'}
