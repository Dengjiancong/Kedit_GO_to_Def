using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Kedit.Console
{
    public partial class App : Application
    {
        internal PetController Pet { get; private set; }
        internal bool Exiting { get; private set; }
        private Mutex singleInstance;
        private Forms.NotifyIcon tray;
        internal DesignPreview Shell;
        internal MainWindow Legacy;

        System.Diagnostics.Process ownerProcess;
        System.Windows.Threading.DispatcherTimer ownerWatch;
        internal bool Owned {get{return ownerProcess!=null;}}
        internal void AttachOwner(IntPtr hwnd){
            if(hwnd==IntPtr.Zero)return;uint pid;GetWindowThreadProcessId(hwnd,out pid);if(pid==0)return;
            var candidate=System.Diagnostics.Process.GetProcessById((int)pid);var identity=candidate.StartTime;var processHandle=candidate.Handle; // Open and retain this specific process instance.
            if(ownerProcess!=null){candidate.Dispose();return;}ownerProcess=candidate;
            if(tray!=null)tray.Visible=false;
            ownerWatch=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};ownerWatch.Tick+=delegate{try{if(!ownerProcess.HasExited)return;}catch{}Exiting=true;Shutdown();};ownerWatch.Start();
        }
        internal int RequestOwnerExit(IntPtr hwnd){uint pid;GetWindowThreadProcessId(hwnd,out pid);if(!Owned||pid!=ownerProcess.Id)return 2;if(Shell!=null&&!Shell.PrepareExit())return 0;Exiting=true;Dispatcher.BeginInvoke(new Action(()=>Shutdown()));return 1;}
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if(Array.IndexOf(e.Args,"--design-preview")>=0 || (Array.IndexOf(e.Args,"--self-test-usage")>=0 && Array.IndexOf(e.Args,"--usage-test-dir")>=0)) { MainWindow=new DesignPreview();MainWindow.Show();return; }
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kedit", "DesktopPet");
            for (int i = 0; i + 1 < e.Args.Length; i++)
                if (e.Args[i] == "--data-dir") data = Path.GetFullPath(e.Args[i + 1]);
            PetRuntime.Configure(data);
            bool isolatedTest = Array.IndexOf(e.Args, "--self-test-pet") >= 0 && Array.IndexOf(e.Args, "--data-dir") >= 0;
            bool f1Test=Array.IndexOf(e.Args,"--self-test-f1")>=0 && Array.IndexOf(e.Args,"--data-dir")>=0;
            bool lifecycleTest=Array.IndexOf(e.Args,"--self-test-lifecycle")>=0 && Array.IndexOf(e.Args,"--data-dir")>=0;
            bool created;
            singleInstance = new Mutex(true, "Local\\Kedit.Console." + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value +
                (isolatedTest || f1Test || lifecycleTest ? ".diagnostics." + System.Diagnostics.Process.GetCurrentProcess().Id : ""), out created);
            if (!created) {
                var existing = FindWindow(null, "Kedit 中控");
                if (existing != IntPtr.Zero) PostMessage(existing, 0x8002, IntPtr.Zero, new IntPtr(StartupRoute(e.Args)));
                Shutdown(); return;
            }
            long parent;if(e.Args.Length>1&&long.TryParse(e.Args[1],out parent)&&!f1Test&&!isolatedTest)AttachOwner(new IntPtr(parent));
            Pet = new PetController();
            var window = new MainWindow();
            if (isolatedTest || f1Test || lifecycleTest) window.Title = "Kedit 中控（独立测试）";
            Legacy=window;
            if(!isolatedTest){new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();Shell=new DesignPreview(true);MainWindow=Shell;}else MainWindow=window;
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开 Kedit 中控", null, delegate { ShowConsole(); });
            menu.Items.Add("显示 / 关闭桌宠", null, delegate { Pet.SetEnabled(!Pet.Settings.Enabled); });
            menu.Items.Add("解除鼠标穿透", null, delegate { Pet.SetOptions(Pet.Settings.Topmost, false); });
            menu.Items.Add("恢复桌宠位置", null, delegate { Pet.ResetPosition(); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出中控与桌宠", null, delegate { ExitConsole(); });
            tray = new Forms.NotifyIcon { Text = "Kedit 中控与桌宠", Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = !Owned };
            tray.DoubleClick += delegate { ShowConsole(); };
            if(Shell!=null){Shell.Show();Shell.OpenRoute(StartupRoute(e.Args),IntPtr.Zero);}else window.Show();
            if (Pet.Settings.Enabled) Pet.SetEnabled(true);
            // Developer-only, explicit diagnostic flag: export our own visual, never the desktop.
            for (int i = 0; i + 1 < e.Args.Length; i++) {
                if (e.Args[i] != "--capture-dir") continue;
                string captureDirectory = Path.GetFullPath(e.Args[i + 1]);
                PetDiagnostics.Run(this, window, captureDirectory, Array.IndexOf(e.Args, "--self-test-pet") >= 0);
            }
        }

        static int StartupRoute(string[] args){int route;for(int i=0;i+1<args.Length;i++)if(args[i]=="--kedit-page" && int.TryParse(args[i+1],out route))return route;return Array.IndexOf(args,"--find-clipboard")>=0?1:0;}
        internal void ShowConsole() { ShowConsole(false,IntPtr.Zero); }
        internal void ShowConsole(bool find,IntPtr source){ShowConsole(find?1:0,source);}
        internal void ShowConsole(int find,IntPtr source) { if(source!=IntPtr.Zero)AttachOwner(source);if(Shell!=null)Shell.OpenRoute(find,source);MainWindow.Show();MainWindow.WindowState=WindowState.Normal;MainWindow.Activate(); }
        internal void ExitConsole() { if(!Exiting && Shell!=null && !Shell.PrepareExit())return;Exiting = true; Shutdown(); }
        protected override void OnExit(ExitEventArgs e)
        {
            Exiting = true;
            if(ownerWatch!=null)ownerWatch.Stop();if(ownerProcess!=null)ownerProcess.Dispose();
            if (Pet != null) Pet.Dispose();
            if (tray != null) tray.Dispose();
            if (singleInstance != null) singleInstance.Dispose();
            base.OnExit(e);
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
