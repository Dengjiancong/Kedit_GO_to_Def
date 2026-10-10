using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Web.Script.Serialization;
namespace Kedit.Console {
 internal static class UpdateInstaller {
  internal sealed class Plan {public string Target, Staged, Backup, Sha;public int Owner, Console;public long OwnerStart,ConsoleStart;}
  internal static string Prepare(UpdateService service,Process owner){
   if(!service.Compiled||owner==null)throw new InvalidOperationException("源码运行模式请使用下载文件手动测试，不会替换 AHK 脚本。");
   string target=Path.GetFullPath(service.TargetPath);if(!File.Exists(target)||!target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new IOException("主程序安装路径无效。");
   if(!string.Equals(owner.MainModule.FileName,target,StringComparison.OrdinalIgnoreCase))throw new IOException("主程序进程与安装目标不一致。");
   string staging=target+".new-"+Guid.NewGuid().ToString("N");File.Copy(service.Package,staging,false);
   try{
    UpdateService.VerifyPackage(staging,new FileInfo(service.Package).Length,null,CancellationToken.None);
    var current=Process.GetCurrentProcess();var plan=new Plan{Target=target,Staged=staging,Backup=target+".previous",Owner=owner.Id,OwnerStart=owner.StartTime.ToUniversalTime().Ticks,Console=current.Id,ConsoleStart=current.StartTime.ToUniversalTime().Ticks};
    using(var hash=System.Security.Cryptography.SHA256.Create())using(var input=File.OpenRead(staging))plan.Sha=BitConverter.ToString(hash.ComputeHash(input)).Replace("-","");
    string folder=Path.Combine(Path.GetDirectoryName(service.Package),"install-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string manifest=Path.Combine(folder,"install.json");File.WriteAllText(manifest,new JavaScriptSerializer().Serialize(plan));
    File.Copy(typeof(UpdateInstaller).Assembly.Location,Path.Combine(folder,"Kedit.Updater.exe"));return manifest;
   }catch{File.Delete(staging);throw;}
  }
  internal static Process Start(string manifest){return Process.Start(new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(manifest),"Kedit.Updater.exe"),"--apply-update \""+manifest+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});}
  internal static void Abort(string manifest){try{File.WriteAllText(manifest+".abort","");}catch{}}
  internal static bool Run(string[] args){if(args.Length!=2||args[0]!="--apply-update")return false;Plan plan=null;bool replaced=false,ownerStopped=false;
   try{
    plan=new JavaScriptSerializer().Deserialize<Plan>(File.ReadAllText(args[1]));
    WaitFor(plan.Owner,plan.OwnerStart,args[1]);WaitFor(plan.Console,plan.ConsoleStart,args[1]);ownerStopped=true;
    if(File.Exists(args[1]+".abort"))throw new OperationCanceledException("安装已取消");
    UpdateService.VerifyPackage(plan.Staged,0,plan.Sha,CancellationToken.None);
    // Same-directory replacement is atomic; retain one rollback executable.
    File.Replace(plan.Staged,plan.Target,plan.Backup,true);replaced=true;
    Process.Start(new ProcessStartInfo(plan.Target){WorkingDirectory=Path.GetDirectoryName(plan.Target),UseShellExecute=true});
   }catch(Exception ex){
    if(replaced&&plan!=null){try{File.Replace(plan.Backup,plan.Target,null,true);Process.Start(plan.Target);}catch(Exception rollback){ex=new IOException(ex.Message+"\n恢复失败："+rollback.Message,ex);}}
    if(!replaced&&ownerStopped&&!(ex is OperationCanceledException)&&plan!=null)try{Process.Start(new ProcessStartInfo(plan.Target){WorkingDirectory=Path.GetDirectoryName(plan.Target),UseShellExecute=true});}catch{}
    if(!(ex is OperationCanceledException))System.Windows.MessageBox.Show("更新未完成，原程序已保留。\n"+ex.Message,"Kedit 更新");
   }finally{if(plan!=null)try{if(File.Exists(plan.Staged))File.Delete(plan.Staged);}catch{}}
   return true;
  }
  static void WaitFor(int id,long started,string manifest){Process process;try{process=Process.GetProcessById(id);}catch(ArgumentException){return;}using(process){if(process.StartTime.ToUniversalTime().Ticks!=started)return;var clock=Stopwatch.StartNew();while(!process.WaitForExit(200)){if(File.Exists(manifest+".abort")||clock.Elapsed.TotalSeconds>60)throw new OperationCanceledException("主程序未退出，已取消安装。");}}}
 }
}
