[CmdletBinding()]
param([switch]$Apply,[string[]]$SkipTag=@(),[switch]$GitOnly)
$arguments=@((Join-Path $PSScriptRoot 'RepositoryMirror.py'),'sync')
if($Apply){$arguments+='--apply'}
if($GitOnly){$arguments+='--git-only'}
foreach($tag in $SkipTag){$arguments+=@('--skip-tag',$tag)}
& python @arguments
if($LASTEXITCODE -ne 0){throw 'Repository sync stopped; inspect the reported conflict or error.'}
