using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace Kedit.Console {
 internal enum UpdateStage { Idle, Connecting, Latest, Available, Downloading, Verifying, Ready, Installing, ConnectionFailed, DownloadFailed, Cancelled }
 internal sealed class UpdateSnapshot {
  public UpdateStage Stage; public string Current="", Latest="", Detail="", Source="NAS · Gitea"; public long Bytes,Total; public DateTime CheckedAt;
  public UpdateSnapshot Copy(){return (UpdateSnapshot)MemberwiseClone();}
 }
 // One task per application; no UI, timer, or subprocess owns the download.
 internal sealed class UpdateService : IDisposable {
  internal const string ReleaseApi="https://gitea.evadd.xyz:88/api/v1/repos/EVADD/Kedit_GO_to_Def/releases/latest";
  internal const string BackupApi="https://api.github.com/repos/Dengjiancong/Kedit_GO_to_Def/releases/latest";
  string activeApi=ReleaseApi;
  internal const string Repository="https://gitea.evadd.xyz:88/EVADD/Kedit_GO_to_Def";
  readonly Func<HttpClient> createClient; readonly string directory;
  CancellationTokenSource cancellation; Task operation; string url,hash,package; long expectedSize;
  internal UpdateSnapshot State=new UpdateSnapshot(); internal event Action Changed;
  internal string ReleaseNotes="";
  internal string TargetPath=""; internal bool Compiled; bool pending;
  internal bool Busy {get{return operation!=null&&!operation.IsCompleted;}}
  internal bool PreviewBlocked {get{return Busy||State.Stage==UpdateStage.Ready||State.Stage==UpdateStage.Installing;}}
  internal bool Available {get{return !string.IsNullOrEmpty(url)&&State.Stage!=UpdateStage.Latest;}}
  internal UpdateService():this(DefaultClient,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Kedit","Updates")){}
  internal UpdateService(Func<HttpClient> factory,string path){createClient=factory;directory=path;}
  internal static HttpClient DefaultClient(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var client=new HttpClient(new HttpClientHandler{UseProxy=true,AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate});client.Timeout=TimeSpan.FromSeconds(30);client.DefaultRequestHeaders.UserAgent.ParseAdd("Kedit-Console/1.0");return client;}
  void Publish(UpdateStage stage,string detail){State.Stage=stage;State.Detail=detail;var changed=Changed;if(changed!=null)changed();}
  internal void Configure(string version,string path,bool compiled){State.Current=version;TargetPath=path;Compiled=compiled;if(State.Stage==UpdateStage.Idle)State.Detail="尚未检查，您可以手动检查更新。";if(pending){pending=false;Check();}else if(Changed!=null)Changed();}
  internal Task Check(){if(Busy||State.Stage==UpdateStage.Ready||State.Stage==UpdateStage.Installing)return operation??Task.FromResult(0);if(string.IsNullOrEmpty(State.Current)){pending=true;Publish(UpdateStage.Idle,"等待主程序提供当前版本信息");return Task.FromResult(0);}if(cancellation!=null)cancellation.Dispose();cancellation=new CancellationTokenSource();operation=CheckCore(cancellation.Token);return operation;}
  async Task CheckCore(CancellationToken token){
   url=null;hash=null;State.Latest="";State.Bytes=State.Total=0;
   try {
    string json=null;Exception last=null;
    for(int attempt=1;attempt<=3;attempt++){
     activeApi=attempt<=2?ReleaseApi:BackupApi;State.Source=attempt<=2?"NAS · Gitea":"GitHub 备用源";
     Publish(UpdateStage.Connecting,attempt<=2?"连接 NAS · Gitea，尝试 "+attempt+" / 2":"NAS 暂时不可用，正在连接 GitHub 备用源");
     try{using(var client=createClient())using(var response=await client.GetAsync(activeApi,token)){response.EnsureSuccessStatusCode();Publish(UpdateStage.Connecting,"正在读取版本与发行文件信息");json=await response.Content.ReadAsStringAsync();}last=null;break;}
     catch(Exception ex){if(token.IsCancellationRequested)throw;last=ex;}
     if(attempt==1)await Task.Delay(800,token);
    }
    if(last!=null)throw last;
    var release=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);State.Latest=StringValue(release,"tag_name");ReleaseNotes=StringValue(release,"body");State.CheckedAt=DateTime.Now;
    if(!IsNewer(State.Latest,State.Current)){Publish(UpdateStage.Latest,"检查完成："+State.CheckedAt.ToString("yyyy-MM-dd HH:mm"));return;}
    var assets=release.ContainsKey("assets")?release["assets"] as System.Collections.IEnumerable:null;
    if(assets!=null)foreach(var item in assets){var asset=item as Dictionary<string,object>;if(asset==null||!StringValue(asset,"name").Equals("Kedit_GO_to_Def.exe",StringComparison.OrdinalIgnoreCase))continue;url=StringValue(asset,"browser_download_url");long.TryParse(StringValue(asset,"size"),out expectedSize);hash=StringValue(asset,"digest");if(hash.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase))hash=hash.Substring(7);else hash="";break;}
    Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https")throw new InvalidDataException("发行版本未提供有效的 HTTPS Kedit_GO_to_Def.exe 文件。");
    if(hash!=""&&!Regex.IsMatch(hash,"^[a-fA-F0-9]{64}$"))throw new InvalidDataException("发行文件的 SHA-256 格式不正确。");
    Publish(UpdateStage.Available,"发现新版本，是否下载并安装？");
   }catch(Exception ex){url=null;Publish(token.IsCancellationRequested?UpdateStage.Cancelled:UpdateStage.ConnectionFailed,token.IsCancellationRequested?"已取消检查":Explain(ex));}
  }
  internal Task Download(){if(Busy||string.IsNullOrEmpty(url)||State.Stage==UpdateStage.Installing)return operation??Task.FromResult(0);if(cancellation!=null)cancellation.Dispose();cancellation=new CancellationTokenSource();operation=DownloadCore(cancellation.Token);return operation;}
  async Task DownloadCore(CancellationToken token){
   string temporary=null;
   try {
    Directory.CreateDirectory(directory);foreach(string stale in Directory.GetFiles(directory,"*.partial"))try{File.Delete(stale);}catch{}temporary=Path.Combine(directory,Guid.NewGuid().ToString("N")+".partial");State.Bytes=0;State.Total=expectedSize;
    Publish(UpdateStage.Downloading,"正在请求 "+State.Source+" 下载文件");
    using(var client=createClient())using(var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token)){
     response.EnsureSuccessStatusCode();if(response.RequestMessage.RequestUri.Scheme!="https")throw new InvalidDataException("下载被重定向到非 HTTPS 地址。");
     long length=response.Content.Headers.ContentLength??0;if(expectedSize>0&&length>0&&expectedSize!=length)throw new InvalidDataException("下载响应大小与发行清单不一致。");State.Total=length>0?length:expectedSize;
     using(var input=await response.Content.ReadAsStreamAsync())using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)){
      var buffer=new byte[65536];var clock=System.Diagnostics.Stopwatch.StartNew();int count;
      while(true){using(var stall=CancellationTokenSource.CreateLinkedTokenSource(token)){var read=input.ReadAsync(buffer,0,buffer.Length,stall.Token);if(await Task.WhenAny(read,Task.Delay(30000,stall.Token))!=read){input.Dispose();ObserveFailedRead(read);token.ThrowIfCancellationRequested();throw new TimeoutException("下载超过 30 秒未收到数据，请重试。");}count=await read;stall.Cancel();}if(count==0)break;await output.WriteAsync(buffer,0,count,token);State.Bytes+=count;if(State.Total>0&&State.Bytes>State.Total)throw new InvalidDataException("下载数据超过声明大小。");if(clock.ElapsedMilliseconds>=100){Publish(UpdateStage.Downloading,"正在下载更新文件");clock.Restart();}}
     }
    }
    token.ThrowIfCancellationRequested();Publish(UpdateStage.Verifying,"检查文件大小、EXE 格式"+(string.IsNullOrEmpty(hash)?"（发行方未提供 SHA-256）":"及 SHA-256"));
    await Task.Run(()=>VerifyPackage(temporary,State.Total,hash,token),token);
    package=Path.ChangeExtension(temporary,".exe");File.Move(temporary,package);temporary=null;
    Publish(UpdateStage.Ready,(State.Total>0?"文件大小、":"未提供预期大小；已完整读取文件，")+"EXE 头部检查通过；"+(string.IsNullOrEmpty(hash)?"未进行 SHA-256 校验。":"SHA-256 校验通过。"));
   }catch(Exception ex){Publish(token.IsCancellationRequested?UpdateStage.Cancelled:UpdateStage.DownloadFailed,token.IsCancellationRequested?"下载已取消，可重新下载":Explain(ex));}
   finally{if(temporary!=null)try{File.Delete(temporary);}catch{}}
  }
  internal static void VerifyPackage(string path,long size,string sha,CancellationToken token){
   using(var input=File.OpenRead(path))using(var reader=new BinaryReader(input)){
    if(size>0&&input.Length!=size)throw new InvalidDataException("文件大小校验失败。");if(input.Length<64||reader.ReadUInt16()!=0x5a4d)throw new InvalidDataException("下载内容不是有效 EXE（可能是网页）。");input.Position=0x3c;int offset=reader.ReadInt32();if(offset<64||offset>input.Length-24)throw new InvalidDataException("EXE 头部损坏。");input.Position=offset;if(reader.ReadUInt32()!=0x4550)throw new InvalidDataException("EXE 格式检查失败。");
    if(!string.IsNullOrEmpty(sha)){input.Position=0;using(var algorithm=SHA256.Create()){byte[] buffer=new byte[65536];int n;while((n=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();algorithm.TransformBlock(buffer,0,n,buffer,0);}algorithm.TransformFinalBlock(buffer,0,0);if(!BitConverter.ToString(algorithm.Hash).Replace("-","").Equals(sha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA-256 校验失败，请重新下载。");}}
   }token.ThrowIfCancellationRequested();
  }
  static void ObserveFailedRead(Task task){task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
  internal void Discard(){if(Busy||State.Stage!=UpdateStage.Ready)return;if(package!=null)try{File.Delete(package);}catch{}package=null;Publish(UpdateStage.Available,"已取消本次安装，可以重新下载。");}
  internal void Cancel(){if(cancellation!=null&&State.Stage!=UpdateStage.Installing)cancellation.Cancel();}
  internal string Package {get{return package;}}
  internal void Installing(){Publish(UpdateStage.Installing,"已检查未保存草稿，正在等待主程序退出并替换文件");}
  internal void InstallFailed(string error){Publish(UpdateStage.Ready,"未安装："+error);}
  internal async Task<string> Diagnose(){var report="更新来源："+State.Source+"\r\n"+activeApi+"\r\n";foreach(string endpoint in new[]{ReleaseApi,activeApi==ReleaseApi?null:activeApi,url}){if(string.IsNullOrEmpty(endpoint))continue;try{using(var client=createClient())using(var request=new HttpRequestMessage(endpoint==ReleaseApi||endpoint==BackupApi?HttpMethod.Get:HttpMethod.Head,endpoint))using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead)){report+=endpoint+"\r\nHTTP "+(int)response.StatusCode+" · "+response.RequestMessage.RequestUri+"\r\n";}}catch(Exception ex){report+=endpoint+"\r\n"+Explain(ex)+"\r\n";}}return report+"\r\n使用系统代理与正常 TLS 证书验证；诊断不下载更新文件，不修改网络配置。";}
  internal static bool IsNewer(string candidate,string installed){var a=VersionParts(candidate);var b=VersionParts(installed);for(int i=0;i<a.Length;i++){if(a[i]!=b[i])return a[i]>b[i];}return false;}
  static int[] VersionParts(string value){var m=Regex.Match(value??"",@"^v?(\d+)\.(\d+)(?:\.(\d+))?(?:-(?:Meme|Amiya)\.v(\d+))?(?:[_-].*)?$",RegexOptions.IgnoreCase);if(!m.Success)throw new InvalidDataException("无法识别版本号："+value);var result=new int[4];for(int i=0;i<4;i++)if(m.Groups[i+1].Success)result[i]=int.Parse(m.Groups[i+1].Value);return result;}
  static string StringValue(Dictionary<string,object> obj,string key){object value;return obj!=null&&obj.TryGetValue(key,out value)?Convert.ToString(value):"";}
  static string Explain(Exception ex){return ex is OperationCanceledException?"连接超时或传输停滞，请重试或运行网络诊断。":ex.GetBaseException().Message;}
  public void Dispose(){Cancel();}
 }
}
