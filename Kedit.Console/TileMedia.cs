using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Kedit.Console {
 internal sealed class TileMedia {
  internal readonly List<BitmapSource> Frames=new List<BitmapSource>();
  internal readonly List<int> Delays=new List<int>();
  internal string Identity;
  static readonly Dictionary<string,TileMedia> cache=new Dictionary<string,TileMedia>();
  internal static TileMedia Load(byte[] bytes,string crop=null){
   var result=new TileMedia();using(var hash=SHA256.Create())result.Identity=Convert.ToBase64String(hash.ComputeHash(bytes))+"|"+crop;
   TileMedia cached;if(cache.TryGetValue(result.Identity,out cached))return cached;
   bool gif=bytes.Length>3&&bytes[0]==71&&bytes[1]==73&&bytes[2]==70;
   if(gif){using(var stream=new MemoryStream(bytes))using(var image=System.Drawing.Image.FromStream(stream)){
    int count=image.GetFrameCount(FrameDimension.Time);byte[] delays=null;try{delays=image.GetPropertyItem(0x5100).Value;}catch(ArgumentException){}
    if(count>600||(long)image.Width*image.Height*count>150000000)throw new IOException("GIF 太大，请使用尺寸或帧数更小的图标素材。");
    for(int i=0;i<count;i++){image.SelectActiveFrame(FrameDimension.Time,i);using(var bitmap=new System.Drawing.Bitmap(image.Width,image.Height)){
     using(var graphics=System.Drawing.Graphics.FromImage(bitmap)){graphics.Clear(System.Drawing.Color.Transparent);graphics.DrawImage(image,0,0,image.Width,image.Height);}
     using(var png=new MemoryStream()){bitmap.Save(png,ImageFormat.Png);png.Position=0;result.Frames.Add(Decode(png));}
    }result.Delays.Add(delays!=null&&delays.Length>=(i+1)*4?Math.Max(20,BitConverter.ToInt32(delays,i*4)*10):100);}
   }}else{using(var stream=new MemoryStream(bytes))result.Frames.Add(Decode(stream));result.Delays.Add(100);}
   if(!string.IsNullOrEmpty(crop)){string[] p=crop.Split('|');if(p.Length!=4||p[0]!="v1")throw new IOException("图标裁剪信息无效");double scale=double.Parse(p[1],CultureInfo.InvariantCulture),x=double.Parse(p[2],CultureInfo.InvariantCulture),y=double.Parse(p[3],CultureInfo.InvariantCulture);for(int i=0;i<result.Frames.Count;i++)result.Frames[i]=PreviewCrop.Crop(result.Frames[i],scale,x,y);}
   if(cache.Count>=16)cache.Clear();cache[result.Identity]=result;return result;
  }
  static BitmapSource Decode(Stream stream){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;}
 }
 internal sealed class AnimatedTileImage : Image {
  readonly DispatcherTimer timer=new DispatcherTimer();readonly TileMedia media;int frame;Window window;
  internal string Identity {get{return media.Identity;}}
  internal AnimatedTileImage(TileMedia value){media=value;Source=media.Frames[0];Stretch=Stretch.UniformToFill;
   timer.Tick+=delegate{frame=(frame+1)%media.Frames.Count;Source=media.Frames[frame];timer.Interval=TimeSpan.FromMilliseconds(media.Delays[frame]);};
   Loaded+=delegate{window=Window.GetWindow(this);if(window!=null)window.StateChanged+=State;Update();};Unloaded+=delegate{timer.Stop();if(window!=null)window.StateChanged-=State;window=null;};IsVisibleChanged+=delegate{Update();};
  }
  void State(object sender,EventArgs e){Update();}
  void Update(){if(IsLoaded&&IsVisible&&window!=null&&window.WindowState!=WindowState.Minimized&&media.Frames.Count>1){timer.Interval=TimeSpan.FromMilliseconds(media.Delays[frame]);timer.Start();}else timer.Stop();}
 }
}
