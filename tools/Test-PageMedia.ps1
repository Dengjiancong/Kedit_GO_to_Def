$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $root '.pet-test\page-media'
$project=Join-Path $fixture 'Kedit.Console'
New-Item -ItemType Directory -Force $project,(Join-Path $fixture 'mp4'),(Join-Path $fixture 'external') | Out-Null
$gif=[Convert]::FromBase64String('R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7')
[IO.File]::WriteAllBytes((Join-Path $fixture 'mp4\tiny.gif'),$gif)
[IO.File]::WriteAllBytes((Join-Path $fixture 'external\tiny.gif'),$gif)
$manifest=Join-Path $fixture 'mp4\pages.json'
[IO.File]::WriteAllText($manifest,'{"home":"tiny.gif","pet.display":"tiny.gif"}')
$source=[Security.SecurityElement]::Escape((Join-Path $root 'Kedit.Console\PageMedia.cs'))
$targets=[Security.SecurityElement]::Escape((Join-Path $root 'Kedit.Console\PageMedia.targets'))
$xml=@"
<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
<PropertyGroup><OutputType>Library</OutputType><TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion><AssemblyName>MediaFixture</AssemblyName><OutputPath>bin\</OutputPath></PropertyGroup>
<ItemGroup><Reference Include="System"/><Reference Include="System.Core"/><Reference Include="System.Web.Extensions"/><Compile Include="$source"/></ItemGroup>
<Import Project="`$(MSBuildToolsPath)\Microsoft.CSharp.targets"/><Import Project="$targets"/>
</Project>
"@
$proj=Join-Path $project 'Kedit.Console.csproj'
[IO.File]::WriteAllText($proj,$xml)
$msbuild='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
$build=& $msbuild $proj /t:Build /v:minimal 2>&1
if($LASTEXITCODE){throw ($build -join "`n")}
$assembly=[Reflection.Assembly]::LoadFile((Join-Path $project 'bin\MediaFixture.dll'))
if(@($assembly.GetManifestResourceNames() | Where-Object {$_ -eq 'PageMedia/tiny.gif'}).Count -ne 1){throw 'duplicate resource'}
$type=$assembly.GetType('Kedit.Console.PageMedia')
$live=$type.GetConstructor(@([string])).Invoke([object[]]@([string]$fixture))
$live.Set('vs.VS_Peek',(Join-Path $fixture 'external\tiny.gif'))
$map=Get-Content $manifest -Raw | ConvertFrom-Json
if($map.'vs.VS_Peek' -eq 'tiny.gif'){throw 'external file did not get collision-safe name'}
$again=$type.GetConstructor(@([string])).Invoke([object[]]@([string]$fixture))
if(!$again.Has('vs.VS_Peek')){throw 'selection not persisted'}
$again.Reset('vs.VS_Peek')
if($again.Has('vs.VS_Peek') -or !$again.Has('home')){throw 'reset affected other pages'}
$packed=$type.GetConstructor(@([string])).Invoke([object[]]@([string](Join-Path $fixture 'not-a-project')))
$path=$packed.Resolve('home','')
if(!(Test-Path $path) -or [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) -ne [Convert]::ToBase64String($gif)){throw 'embedded extraction failed'}
[IO.File]::WriteAllText($manifest,'{"pet.follow":"missing.mp4"}')
$failure=& $msbuild $proj /t:Build /v:minimal 2>&1
if(!$LASTEXITCODE -or ($failure -join '') -notmatch 'pet.follow'){throw 'missing media did not fail with page name'}
[IO.File]::WriteAllBytes((Join-Path $fixture 'mp4\broken.mp4'),[byte[]](0,0,0,32,102,116,121,112))
[IO.File]::WriteAllText($manifest,'{"vs.VS_Peek":"broken.mp4"}')
$failure=& $msbuild $proj /t:Build /v:minimal 2>&1
if(!$LASTEXITCODE -or ($failure -join '') -notmatch 'vs.VS_Peek'){throw 'corrupt MP4 did not fail with page name'}
'PASS: persisted mapping, external copy, page reset, deduplicated embedding, standalone extraction, missing/corrupt build errors'
