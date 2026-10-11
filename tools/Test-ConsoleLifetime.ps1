$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dir=Join-Path $root ('.pet-test\lifetime-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $dir | Out-Null
$source=@'
using System;using System.Windows.Forms;using System.IO;
class Owner { [STAThread] static void Main(string[] args){var form=new Form();var handle=form.Handle;File.WriteAllText(args[0],handle.ToInt64().ToString());Application.Run();}}
'@
[IO.File]::WriteAllText("$dir\Owner.cs",$source)
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /r:System.Windows.Forms.dll /out:"$dir\Owner.exe" "$dir\Owner.cs"
if($LASTEXITCODE -ne 0){throw 'Owner fixture compile failed'}
Add-Type @'
using System;using System.Runtime.InteropServices;using System.Text;
public static class LifetimeNative {
 public delegate bool Visit(IntPtr h,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumWindows(Visit v,IntPtr data);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
 [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h,uint msg,IntPtr w,IntPtr l,uint flags,uint ms,out IntPtr result);
 public static IntPtr Find(int id){IntPtr found=IntPtr.Zero;EnumWindows((h,d)=>{uint pid;GetWindowThreadProcessId(h,out pid);var text=new StringBuilder(256);GetWindowText(h,text,256);if(pid==id&&text.ToString().Contains("独立测试"))found=h;return true;},IntPtr.Zero);return found;}
}
'@
foreach($mode in @('normal','abnormal','independent')) {
 $owner=$null;$child=$null
 try {
  $handle=0
  if($mode -ne 'independent'){
   $owner=Start-Process "$dir\Owner.exe" -ArgumentList ('"'+$dir+'\'+$mode+'.txt"') -WindowStyle Hidden -PassThru
   for($i=0;$i -lt 50 -and !(Test-Path "$dir\$mode.txt");$i++){Start-Sleep -Milliseconds 100}
   $handle=[long](Get-Content "$dir\$mode.txt")
  }
  $arguments='side.mp4 '+$handle+' --self-test-lifecycle --background-update --data-dir "'+$dir+'\'+$mode+'"'
  $child=Start-Process "$root\Kedit.Console\bin\Release\Kedit.Console.exe" -ArgumentList $arguments -WindowStyle Hidden -PassThru
  $window=[IntPtr]::Zero
  for($i=0;$i -lt 100 -and $window -eq [IntPtr]::Zero;$i++){Start-Sleep -Milliseconds 100;$window=[LifetimeNative]::Find($child.Id);if($child.HasExited){throw 'Child exited before test'}}
  if($window -eq [IntPtr]::Zero){throw 'Child fixture window unavailable'}
  if($mode -eq 'normal'){
   $answer=[IntPtr]::Zero
   $sent=[LifetimeNative]::SendMessageTimeout($window,0x8003,[IntPtr]$handle,[IntPtr]::Zero,2,5000,[ref]$answer)
   if($sent -eq [IntPtr]::Zero -or $answer.ToInt64() -ne 1){throw 'Normal owner exit was not acknowledged'}
   if(!$child.WaitForExit(10000)){throw 'Normal owner exit left child running'}
  } elseif($mode -eq 'abnormal') {
   $owner.Kill();$owner.WaitForExit()
   if(!$child.WaitForExit(10000)){throw 'Terminated owner left child running'}
  } else {
   Start-Sleep -Milliseconds 1500
   if($child.HasExited){throw 'Independent console incorrectly exited'}
  }
  "PASS: $mode"
 } finally {
  if($child -and !$child.HasExited){$child.Kill();$child.WaitForExit()}
  if($owner -and !$owner.HasExited){$owner.Kill();$owner.WaitForExit()}
 }
}
