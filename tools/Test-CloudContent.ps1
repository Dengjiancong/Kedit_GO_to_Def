$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dir=Join-Path $root '.pet-test/cloud-content'
New-Item -ItemType Directory -Force $dir | Out-Null
$code=@'
using System;
using System.IO;
using System.Linq;
using System.Threading;
namespace Kedit.Console {
internal static class PetRuntime {public static string DataDirectory;}
class CloudCheck {
 [STAThread] static int Main(string[] args){try{PetRuntime.DataDirectory=args[0];var embedded=CloudContent.Cached<HomeContent>("home.json");if(embedded.carousel.items.Length!=3)throw new Exception("embedded home");
 var home=CloudContent.Refresh<HomeContent>("home.json").GetAwaiter().GetResult();foreach(var item in home.carousel.items){var picture=CloudContent.Picture(item.image_url).GetAwaiter().GetResult();if(picture.PixelWidth==0)throw new Exception("image decode");}
 var models=CloudContent.Refresh<ModelContent>("models.json").GetAwaiter().GetResult();var model=models.models.First(x=>x.id==models.default_model_id);string installed=CloudContent.Install(model,CancellationToken.None,null).GetAwaiter().GetResult();PetModel.Validate(installed);
 var package=CloudContent.Fetch(model.download_url,128*1024*1024,CancellationToken.None,null).GetAwaiter().GetResult();var local=Path.Combine(args[0],"company.zip");File.WriteAllBytes(local,package);model.fallback_path=local;string publicUrl=model.download_url;
 CloudContent.Install(model,CancellationToken.None,null,true).GetAwaiter().GetResult();model.download_url="https://127.0.0.1:1/unavailable";CloudContent.Install(model,CancellationToken.None,null).GetAwaiter().GetResult();
 model.download_url=publicUrl;model.fallback_path=Path.Combine(args[0],"missing.zip");CloudContent.Install(model,CancellationToken.None,null).GetAwaiter().GetResult();model.fallback_path=local;
 model.sha256=new string('0',64);bool rejected=false;try{CloudContent.Install(model,CancellationToken.None,null).GetAwaiter().GetResult();}catch(IOException){rejected=true;}if(!rejected)throw new Exception("hash mismatch accepted");
 var cancelled=new CancellationTokenSource();cancelled.Cancel();try{CloudContent.Install(model,cancelled.Token,null).GetAwaiter().GetResult();throw new Exception("cancel ignored");}catch(OperationCanceledException){}
 File.WriteAllText(Path.Combine(CloudContent.Cache,"home.json"),"broken");if(CloudContent.Cached<HomeContent>("home.json").carousel.items.Length!=3)throw new Exception("fallback");
 System.Console.WriteLine("PASS: live manifests, three decoded images, cached/default fallback, verified ZIP installation with Chinese filenames, hash rejection, cancellation");return 0;
 }catch(Exception e){System.Console.WriteLine(e);return 1;}}
}}
'@
$source=Join-Path $dir 'CloudCheck.cs'
[IO.File]::WriteAllText($source,$code,[Text.UTF8Encoding]::new($true))
$framework='C:/Windows/Microsoft.NET/Framework64/v4.0.30319'
$wpf=Join-Path $framework 'WPF'
& "$framework/csc.exe" /nologo /target:exe "/out:$dir/CloudCheck.exe" /r:System.Net.Http.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll "/r:$wpf/PresentationCore.dll" "/r:$wpf/WindowsBase.dll" /r:System.Xaml.dll "/resource:$root/Gitea/home.json,CloudDefaults/home.json" "/resource:$root/Gitea/models.json,CloudDefaults/models.json" $source "$root\Kedit.Console\CloudContent.cs" "$root\Kedit.Console\Pet\PetModel.cs"
if($LASTEXITCODE){throw 'compile failed'}
& "$dir/CloudCheck.exe" $dir
if($LASTEXITCODE){throw 'cloud content tests failed'}
