[CmdletBinding()]
param(
    [string]$SourceScript = 'Kedit_GO_to_Def_ver18.30.ahk',
    [string]$OutputDirectory,
    [string]$CompilerPath = 'C:\Program Files\AutoHotkey\Compiler\Ahk2Exe.exe'
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$source = Get-Item -LiteralPath (Join-Path $projectRoot $SourceScript) -ErrorAction Stop
$baseExe = Join-Path $projectRoot 'AutoHotkey.exe'
$iconPath = Join-Path $projectRoot 'sikadi.ico'
$monitorPath = Join-Path $projectRoot 'MSTSC_Monitor.exe'

foreach ($required in @($baseExe, $iconPath, $monitorPath, $CompilerPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required build file is missing: $required"
    }
}

$sourceText = Get-Content -LiteralPath $source.FullName -Encoding UTF8 -Raw
$versionMatch = [regex]::Match($sourceText, '(?im)^\s*Global\s+CurrentVersion\s*:=\s*"(?<version>[^"]+)"')
if (-not $versionMatch.Success) {
    throw 'CurrentVersion was not found in the AutoHotkey source.'
}
$version = $versionMatch.Groups['version'].Value
if ($version -notmatch '^[A-Za-z0-9._-]+$') {
    throw "CurrentVersion contains invalid filename characters: $version"
}

if (-not $OutputDirectory) {
    $OutputDirectory = $projectRoot
}
$outputDirectoryFull = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $outputDirectoryFull)) {
    New-Item -ItemType Directory -Path $outputDirectoryFull -Force | Out-Null
}
$outputPath = Join-Path $outputDirectoryFull ("Kedit_GO_to_Def_$version.exe")
$outputProcessName = [System.IO.Path]::GetFileNameWithoutExtension($outputPath)
if (Get-Process -Name $outputProcessName -ErrorAction SilentlyContinue) {
    throw "The output EXE is running. Close it before rebuilding: $outputPath"
}

Write-Host "Building $version from $($source.Name)"
Push-Location -LiteralPath $projectRoot
try {
    $syntaxArgs = '/ErrorStdOut /iLib NUL "{0}"' -f $source.FullName
    $syntaxProcess = Start-Process -FilePath $baseExe -ArgumentList $syntaxArgs `
        -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
    if ($syntaxProcess.ExitCode -ne 0) {
        throw "AutoHotkey syntax check failed with exit code $($syntaxProcess.ExitCode)."
    }

    $compilerArgs = '/in "{0}" /out "{1}" /icon "{2}" /base "{3}"' -f `
        $source.FullName, $outputPath, $iconPath, $baseExe
    $compilerProcess = Start-Process -FilePath $CompilerPath -ArgumentList $compilerArgs `
        -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
    if ($compilerProcess.ExitCode -ne 0) {
        throw "Ahk2Exe failed with exit code $($compilerProcess.ExitCode)."
    }
} finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
    throw "Compiler reported success but output EXE was not found: $outputPath"
}

$output = Get-Item -LiteralPath $outputPath
[pscustomobject]@{
    Version = $version
    Output = $output.FullName
    OutputBytes = $output.Length
    OutputSHA256 = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash
    MonitorSHA256 = (Get-FileHash -LiteralPath $monitorPath -Algorithm SHA256).Hash
}
