using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Kedit.Console;
class TestUpdateService {
 sealed class UnknownContent:HttpContent {readonly byte[] bytes;internal UnknownContent(byte[] data){bytes=data;}protected override bool TryComputeLength(out long length){length=0;return false;}protected override Task SerializeToStreamAsync(Stream stream,TransportContext context){return stream.WriteAsync(bytes,0,bytes.Length);}}
 sealed class Handler:HttpMessageHandler {
  internal byte[] Payload;internal string Digest="",Tag="v19.00-Amiya.v009";internal bool Fail,Slow,NoLength;internal int Calls;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;if(Slow)await Task.Delay(10000,token);if(Fail)throw new HttpRequestException("offline fixture");
   HttpContent content;if(request.RequestUri.AbsolutePath.EndsWith("latest"))content=new StringContent(new JavaScriptSerializer().Serialize(new {tag_name=Tag,assets=new[]{new{name="Kedit_GO_to_Def.exe",browser_download_url="https://fixture.invalid/update.exe",size=NoLength?0:Payload.Length,digest=Digest}}}));else content=NoLength?(HttpContent)new UnknownContent(Payload):new ByteArrayContent(Payload);
   return new HttpResponseMessage(HttpStatusCode.OK){Content=content,RequestMessage=request};
  }
 }
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
 static string Hash(byte[] data){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(data)).Replace("-","");}
 static UpdateService Service(Handler handler,string directory){return new UpdateService(()=>new HttpClient(handler,false),directory);}
 static void Main(string[] args){try{Run(args).GetAwaiter().GetResult();System.Console.WriteLine("PASS: update service, cancellation, failures, versioning, verification, isolated installer replacement/abort");}catch(Exception ex){System.Console.WriteLine(ex);Environment.ExitCode=1;}}
 static async Task Run(string[] args){string root=args[0];Directory.CreateDirectory(root);byte[] exe=File.ReadAllBytes(args[1]);
  Assert(UpdateService.IsNewer("v19.00-Amiya.v009","v19.00-Amiya.v008"),"Amiya revision");Assert(!UpdateService.IsNewer("v19.00-Amiya.v008","v19.00-Amiya.v008"),"same version");Assert(UpdateService.IsNewer("v18.30-Meme.v100_for_test","v18.30-Meme.v009"),"legacy version");
  var handler=new Handler{Payload=exe,Digest="sha256:"+Hash(exe)};using(var service=Service(handler,root)){
   await service.Check();Assert(handler.Calls==0,"check before metadata");service.Configure("v19.00-Amiya.v008","source.ahk",false);while(service.Busy)await Task.Delay(5);Assert(service.State.Stage==UpdateStage.Available,"available");
   await service.Download();Assert(service.State.Stage==UpdateStage.Ready&&File.Exists(service.Package),"download verified");Assert(service.State.Detail.Contains("SHA-256"),"hash detail");int count=handler.Calls;await service.Check();Assert(handler.Calls==count,"ready check must reuse");service.Discard();Assert(service.State.Stage==UpdateStage.Available,"discard");
  }
  handler=new Handler{Payload=exe,Digest="sha256:"+new string('0',64)};using(var service=Service(handler,root)){service.Configure("v19.00-Amiya.v008","",false);await service.Check();await service.Download();Assert(service.State.Stage==UpdateStage.DownloadFailed&&service.State.Detail.Contains("SHA-256"),"bad digest");}
  handler=new Handler{Payload=System.Text.Encoding.UTF8.GetBytes("<html>error</html>")};using(var service=Service(handler,root)){service.Configure("v19.00-Amiya.v008","",false);await service.Check();await service.Download();Assert(service.State.Stage==UpdateStage.DownloadFailed,"html is not exe");}
  handler=new Handler{Payload=exe,Fail=true};using(var service=Service(handler,root)){service.Configure("v19.00-Amiya.v008","",false);await service.Check();Assert(service.State.Stage==UpdateStage.ConnectionFailed&&handler.Calls==3&&service.State.Source.Contains("GitHub"),"bounded retry and backup");}
  handler=new Handler{Payload=exe,Slow=true};using(var service=Service(handler,root)){service.Configure("v19.00-Amiya.v008","",false);var first=service.Check();var second=service.Check();Assert(object.ReferenceEquals(first,second)&&handler.Calls==1,"single active task");service.Cancel();await first;Assert(service.State.Stage==UpdateStage.Cancelled&&handler.Calls==1,"cancel no retry");}
  handler=new Handler{Payload=exe,Tag="v19.00-Amiya.v008"};using(var service=Service(handler,root)){service.Configure("v19.00-Amiya.v008","",false);await service.Check();Assert(service.State.Stage==UpdateStage.Latest,"latest");}
  handler=new Handler{Payload=exe};using(var service=Service(handler,root)){service.Configure("v19.00-Amiya.v008","",false);await service.Check();await service.Download();Assert(service.State.Stage==UpdateStage.Ready&&service.State.Detail.Contains("未进行"),"no fake hash success");}
  handler=new Handler{Payload=exe,NoLength=true};using(var service=Service(handler,root)){service.Configure("v19.00-Amiya.v008","",false);await service.Check();await service.Download();Assert(service.State.Stage==UpdateStage.Ready&&service.State.Total==0&&service.State.Detail.Contains("未提供预期大小"),"unknown length honest detail");}
  handler=new Handler{Payload=exe};using(var service=Service(handler,root)){service.Configure("v19.00-Amiya.v008","",false);await service.Check();handler.Slow=true;var task=service.Download();service.Cancel();await task;Assert(service.State.Stage==UpdateStage.Cancelled,"cancel download");}
  Assert(Directory.GetFiles(root,"*.partial").Length==0,"partial cleanup");
  // Only disposable fixture executables are replaced; never invoke the real owner.
  string target=Path.Combine(root,"fixture.exe"),staged=target+".new",backup=target+".backup",manifest=Path.Combine(root,"install.json");File.Copy(args[1],target,true);File.Copy(args[1],staged,true);
  var plan=new UpdateInstaller.Plan{Target=target,Staged=staged,Backup=backup,Sha=Hash(exe),Owner=int.MaxValue,Console=int.MaxValue};File.WriteAllText(manifest,new JavaScriptSerializer().Serialize(plan));UpdateInstaller.Run(new[]{"--apply-update",manifest});
  for(int i=0;i<100&&!File.Exists(Path.Combine(root,"ran.txt"));i++)await Task.Delay(20);Assert(File.Exists(backup)&&File.Exists(Path.Combine(root,"ran.txt")),"installer replacement and restart fixture");
  File.Copy(args[1],staged,true);File.WriteAllText(manifest+".abort","");UpdateInstaller.Run(new[]{"--apply-update",manifest});Assert(!File.Exists(staged)&&File.Exists(target),"abort preserves target");
 }
}
