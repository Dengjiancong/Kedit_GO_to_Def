[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$package = Join-Path $root '.packages\Microsoft.Web.WebView2.1.0.3537.50'
$vendor = Join-Path $root 'Kedit.Console\PetWeb\vendor'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
New-Item -ItemType Directory -Force -Path $package, $vendor | Out-Null
$lockPath = Join-Path $PSScriptRoot 'PetDependencies.lock.json'
$dependencyHashes = Get-Content -LiteralPath $lockPath -Raw -Encoding UTF8 | ConvertFrom-Json
function Assert-DependencyHash($path) {
    $relative = $path.Substring($root.Length + 1).Replace('\', '/')
    $expected = $dependencyHashes.$relative
    if (-not $expected -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $expected) {
        throw "Dependency checksum mismatch: $relative. Remove this cached file and restore again; do not silently accept changed upstream content."
    }
}
function Get-Dependency($url, $destination) {
    if (Test-Path -LiteralPath $destination) { Assert-DependencyHash $destination; return }
    Write-Host "Downloading $url"
    Invoke-WebRequest -UseBasicParsing -Uri $url -UserAgent 'Mozilla/5.0' -Headers @{ Referer = 'https://www.live2d.com/' } -OutFile ($destination + '.download')
    Move-Item -LiteralPath ($destination + '.download') -Destination $destination -Force
    Assert-DependencyHash $destination
}
$archive = Join-Path $package 'package.zip'
Get-Dependency 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.3537.50/microsoft.web.webview2.1.0.3537.50.nupkg' $archive
if (-not (Test-Path (Join-Path $package 'lib\net462\Microsoft.Web.WebView2.Wpf.dll'))) {
    Expand-Archive -LiteralPath $archive -DestinationPath $package -Force
}
Get-Dependency 'https://cdn.jsdelivr.net/npm/pixi.js@6.5.10/dist/browser/pixi.min.js' (Join-Path $vendor 'pixi.min.js')
Get-Dependency 'https://cdn.jsdelivr.net/npm/pixi-live2d-display@0.4.0/dist/cubism4.min.js' (Join-Path $vendor 'cubism4.min.js')
Get-Dependency 'https://cubism.live2d.com/sdk-web/core/05/live2dcubismcore.min.js' (Join-Path $vendor 'live2dcubismcore.min.js')
Get-Dependency 'https://cdn.jsdelivr.net/npm/pixi.js@6.5.10/LICENSE' (Join-Path $vendor 'PIXI-LICENSE.txt')
Get-Dependency 'https://cdn.jsdelivr.net/npm/pixi-live2d-display@0.4.0/LICENSE' (Join-Path $vendor 'DISPLAY-LICENSE.txt')
Write-Host 'Desktop pet dependencies ready.'
