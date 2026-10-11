using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
namespace Kedit.Console {
 // WPF image creation/cropping stays on one background STA; only frozen pixels cross threads.
 internal static class MediaWork {
  static readonly BlockingCollection<Action> queue=new BlockingCollection<Action>();
  static MediaWork(){var thread=new Thread(()=>{foreach(var action in queue.GetConsumingEnumerable())action();}){IsBackground=true,Name="Kedit media preparation"};thread.SetApartmentState(ApartmentState.STA);thread.Start();}
  internal static Task<T> Run<T>(Func<T> action){var done=new TaskCompletionSource<T>();queue.Add(()=>{try{done.SetResult(action());}catch(Exception ex){done.SetException(ex);}});return done.Task;}
 }
 internal static class MediaTiming {
  static readonly object gate=new object();
  internal static void Record(string phase,string page,long milliseconds){ThreadPool.QueueUserWorkItem(delegate{try{lock(gate){string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Kedit","Diagnostics");Directory.CreateDirectory(folder);string file=Path.Combine(folder,"page-performance.log");if(File.Exists(file)&&new FileInfo(file).Length>524288)File.WriteAllText(file,"");File.AppendAllText(file,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+" "+phase+" page="+page+" ms="+milliseconds+Environment.NewLine);}}catch{}});}
 }
 internal sealed class PreparedBackground {
  internal string Path,Identity;internal BitmapSource Still;
  internal System.Collections.Generic.List<BitmapFrame> Frames;
  internal static PreparedBackground Read(string path,string identity){var result=new PreparedBackground{Path=path,Identity=identity};if(identity==null)return result;string ext=System.IO.Path.GetExtension(path).ToLowerInvariant();
   if(ext==".gif"){using(var stream=File.OpenRead(path)){var decoder=new GifBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);result.Frames=new System.Collections.Generic.List<BitmapFrame>();foreach(var frame in decoder.Frames){frame.Freeze();result.Frames.Add(frame);}}}
   else if(ext==".png"||ext==".jpg"||ext==".jpeg"){using(var stream=File.OpenRead(path))result.Still=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];result.Still.Freeze();}
   return result;
  }
 }
 internal sealed partial class DesignPreview {
  async Task CheckMediaLoading(string directory){
   string first=Path.Combine(directory,"async-first.gif"),last=Path.Combine(directory,"async-last.gif"),alias=Path.Combine(directory,"async-alias.gif");
   File.WriteAllBytes(first,Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"));
   File.WriteAllBytes(last,Convert.FromBase64String("R0lGODlhAgABAIEAAAAAAP8AAAAA/////yH5BAkIAAAALAAAAAACAAEAAAICDFEAIfkECRAAAAAsAAAAAAIAAQAAAgIEVQA7"));File.Copy(last,alias,true);
   currentMediaPage="diagnostics.media";var gate=new ManualResetEventSlim(false);var blocker=MediaWork.Run(()=>{gate.Wait(5000);return true;});
   var oldSource=gif.Source;LoadPageMedia(first);LoadPageMedia(last);
   await Task.Delay(50);if(gif.Source!=oldSource)throw new Exception("Background changed before preparation completed");gate.Set();await blocker;await MediaWork.Run(()=>true);await Task.Delay(100);gate.Dispose();
   if(decoder==null||decoder.Frames.Count!=2||gif.Source==null||!decoder.Frames[0].IsFrozen||playingMediaIdentity!=MediaIdentity(last))throw new Exception("Async GIF / latest page result failed: "+status.Text);
   var original=decoder;paused=true;Playback();LoadPageMedia(alias);await MediaWork.Run(()=>true);await Task.Delay(100);
   if(!object.ReferenceEquals(original,decoder)||!paused)throw new Exception("Same GIF restarted after async loading");
   // A delayed preparation must not override an explicit newer preview/load.
   gate=new ManualResetEventSlim(false);blocker=MediaWork.Run(()=>{gate.Wait(5000);return true;});LoadPageMedia(first);LoadMedia(last);gate.Set();await blocker;await MediaWork.Run(()=>true);await Task.Delay(100);gate.Dispose();
   if(playingMediaIdentity!=MediaIdentity(last))throw new Exception("Stale background overwrote newer request");Navigate("首页");
  }
 }

}
