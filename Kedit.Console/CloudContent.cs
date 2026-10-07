using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.IO.Compression;
using System.Windows.Media.Imaging;

namespace Kedit.Console {
    public sealed class ContentItem { public string id,title,image_url,target_url,date; }
    public sealed class ContentTab { public string id,title; public ContentItem[] items; }
    public sealed class CarouselContent { public int interval_seconds; public string direction; public bool pause_on_hover; public ContentItem[] items; }
    public sealed class HomeContent { public int schema_version; public CarouselContent carousel; public ContentTab[] tabs; }
    public sealed class ModelAuthor { public string name,url,platform; }
    public sealed class DownloadModel { public string id,name,version,size_display,sha256,download_url,share_url,archive_format,model_entry,original_download_page; public long? size_bytes; public ModelAuthor author; }
    public sealed class ModelContent { public int schema_version; public string default_model_id; public DownloadModel[] models; }
    internal static class CloudContent {
        const string Origin="https://gitea.evadd.xyz:88/EVADD/Kedit_Content/raw/branch/main/";
        static readonly HttpClient client=CreateClient();
        static HttpClient CreateClient(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;return new HttpClient {Timeout=TimeSpan.FromSeconds(90)};}
        internal static string Cache {get{return Path.Combine(PetRuntime.DataDirectory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Kedit"),"CloudContent");}}
        public static bool WebUrl(string url){Uri uri;return Uri.TryCreate(url,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.Scheme=="http");}
        public static void Open(string url){if(WebUrl(url))System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});}
        public static T Read<T>(string json){return new JavaScriptSerializer {MaxJsonLength=1024*1024}.Deserialize<T>(json);}
        static void Validate(string name,string json){
            if(name=="home.json"){
                var h=Read<HomeContent>(json);if(h==null||h.schema_version!=1||h.carousel==null||h.carousel.items==null||h.carousel.items.Length>20||h.tabs==null||h.tabs.Length>10)throw new InvalidDataException("公告清单格式不正确。");
                foreach(var i in h.carousel.items)if(i==null||!WebUrl(i.image_url)||!WebUrl(i.target_url)||string.IsNullOrWhiteSpace(i.title))throw new InvalidDataException("轮播链接无效。");
                foreach(var t in h.tabs){if(t==null||t.items==null||t.items.Length>100||string.IsNullOrWhiteSpace(t.title))throw new InvalidDataException("栏目无效。");foreach(var i in t.items)if(i==null||!WebUrl(i.target_url)||string.IsNullOrWhiteSpace(i.title))throw new InvalidDataException("公告条目无效。");}
            }else{var m=Read<ModelContent>(json);if(m==null||m.schema_version!=1||m.models==null||!m.models.Any(x=>x!=null&&x.id==m.default_model_id))throw new InvalidDataException("模型清单格式不正确。");foreach(var x in m.models){if(x==null||string.IsNullOrWhiteSpace(x.id)||string.IsNullOrWhiteSpace(x.name)||!WebUrl(x.share_url)||!WebUrl(x.original_download_page)||(!string.IsNullOrEmpty(x.download_url)&&(!WebUrl(x.download_url)||x.archive_format!="zip"||!System.Text.RegularExpressions.Regex.IsMatch(x.sha256??"","\\A[0-9A-Fa-f]{64}\\z"))))throw new InvalidDataException("模型下载信息无效。");}}
        }
        public static T Cached<T>(string name){try{string j=File.ReadAllText(Path.Combine(Cache,name));Validate(name,j);return Read<T>(j);}catch{using(var s=typeof(CloudContent).Assembly.GetManifestResourceStream("CloudDefaults/"+name))using(var r=new StreamReader(s)){string j=r.ReadToEnd();Validate(name,j);return Read<T>(j);}}}
        public static async Task<T> Refresh<T>(string name){string j=Encoding.UTF8.GetString(await Fetch(Origin+name,1024*1024,CancellationToken.None,null));j=j.TrimStart('\uFEFF');Validate(name,j);Directory.CreateDirectory(Cache);string path=Path.Combine(Cache,name),tmp=path+".tmp";File.WriteAllText(tmp,j,Encoding.UTF8);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);return Read<T>(j);}
        public static async Task<byte[]> Fetch(string url,int limit,CancellationToken cancel,IProgress<int> progress){
            if(!WebUrl(url))throw new InvalidDataException("下载地址无效。");
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel)){timeout.CancelAfter(TimeSpan.FromMinutes(3));var token=timeout.Token;
            using(var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token)){response.EnsureSuccessStatusCode();long total=response.Content.Headers.ContentLength??0;if(total>limit)throw new InvalidDataException("文件超过大小限制。");using(var input=await response.Content.ReadAsStreamAsync())using(var output=new MemoryStream()){var buffer=new byte[65536];int count;while((count=await input.ReadAsync(buffer,0,buffer.Length,token))>0){if(output.Length+count>limit)throw new InvalidDataException("文件超过大小限制。");output.Write(buffer,0,count);if(progress!=null)progress.Report(total>0?(int)(output.Length*100/total):-1);}return output.ToArray();}}}
        }
        static string Hash(byte[] bytes){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(bytes)).Replace("-","");}
        public static BitmapImage Decode(byte[] bytes){using(var s=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=900;image.StreamSource=s;image.EndInit();image.Freeze();return image;}}
        public static async Task<BitmapImage> Picture(string url){Directory.CreateDirectory(Cache);string path=Path.Combine(Cache,Hash(Encoding.UTF8.GetBytes(url))+".image");if(File.Exists(path)){try{return Decode(await Task.Run(()=>File.ReadAllBytes(path)));}catch{}}
            var bytes=await Fetch(url,16*1024*1024,CancellationToken.None,null);var result=Decode(bytes);await Task.Run(()=>File.WriteAllBytes(path,bytes));return result;}
        public static async Task<string> Install(DownloadModel model,CancellationToken cancel,IProgress<int> progress){
            if(model==null||model.archive_format!="zip"||!WebUrl(model.download_url)||string.IsNullOrEmpty(model.sha256)||!System.Text.RegularExpressions.Regex.IsMatch(model.sha256,"\\A[0-9A-Fa-f]{64}\\z"))throw new InvalidDataException("尚未配置有效的 ZIP 下载地址和校验值，请使用原下载入口。");
            var bytes=await Fetch(model.download_url,128*1024*1024,cancel,progress);
            return await Task.Run(()=>{cancel.ThrowIfCancellationRequested();if(!string.Equals(Hash(bytes),model.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("模型 SHA-256 校验失败，未安装。");if(model.size_bytes.HasValue&&bytes.LongLength!=model.size_bytes.Value)throw new InvalidDataException("模型文件大小不匹配。");
                string directory=Path.Combine(Cache,"Models",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
                try{using(var memory=new MemoryStream(bytes))using(var zip=new ZipArchive(memory,ZipArchiveMode.Read,false,Encoding.GetEncoding(936))){long total=0;if(zip.Entries.Count>5000)throw new InvalidDataException("压缩包文件过多。");foreach(var entry in zip.Entries){cancel.ThrowIfCancellationRequested();total+=entry.Length;if(total>512L*1024*1024)throw new InvalidDataException("解压文件过大。");string relative=entry.FullName.Replace('/',Path.DirectorySeparatorChar);string path=Path.GetFullPath(Path.Combine(directory,relative));if(relative.Contains(":")||!path.StartsWith(directory+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("压缩包包含越界路径。");if(string.IsNullOrEmpty(entry.Name)){Directory.CreateDirectory(path);continue;}Directory.CreateDirectory(Path.GetDirectoryName(path));using(var input=entry.Open())using(var output=File.Create(path)){var buffer=new byte[65536];int count;while((count=input.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();output.Write(buffer,0,count);}}}}
                    var paths=Directory.GetFiles(directory,"*.model3.json",SearchOption.AllDirectories);if(paths.Length!=1)throw new InvalidDataException("压缩包必须包含唯一模型入口，请手动下载选择。");PetModel.Validate(paths[0]);cancel.ThrowIfCancellationRequested();return paths[0];
                }catch{Directory.Delete(directory,true);throw;}
            },cancel);
        }
    }
}
