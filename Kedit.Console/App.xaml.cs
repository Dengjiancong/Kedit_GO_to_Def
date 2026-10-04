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

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kedit", "DesktopPet");
            for (int i = 0; i + 1 < e.Args.Length; i++)
                if (e.Args[i] == "--data-dir") data = Path.GetFullPath(e.Args[i + 1]);
            PetRuntime.Configure(data);
            bool created;
            singleInstance = new Mutex(true, "Local\\Kedit.Console." + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value, out created);
            if (!created) {
                var existing = FindWindow(null, "Kedit 中控");
                if (existing != IntPtr.Zero) PostMessage(existing, 0x8002, IntPtr.Zero, IntPtr.Zero);
                Shutdown(); return;
            }
            Pet = new PetController();
            var window = new MainWindow();
            MainWindow = window;
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开 Kedit 中控", null, delegate { ShowConsole(); });
            menu.Items.Add("显示 / 关闭桌宠", null, delegate { Pet.SetEnabled(!Pet.Settings.Enabled); });
            menu.Items.Add("解除鼠标穿透", null, delegate { Pet.SetOptions(Pet.Settings.Topmost, false); });
            menu.Items.Add("恢复桌宠位置", null, delegate { Pet.ResetPosition(); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出中控与桌宠", null, delegate { ExitConsole(); });
            tray = new Forms.NotifyIcon { Text = "Kedit 中控与桌宠", Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { ShowConsole(); };
            window.Show();
            if (Pet.Settings.Enabled) Pet.SetEnabled(true);
            // Developer-only, explicit diagnostic flag: export our own visual, never the desktop.
            for (int i = 0; i + 1 < e.Args.Length; i++) {
                if (e.Args[i] != "--capture-dir") continue;
                string captureDirectory = Path.GetFullPath(e.Args[i + 1]);
                PetDiagnostics.Run(this, window, captureDirectory, Array.IndexOf(e.Args, "--self-test-pet") >= 0);
            }
        }

        internal void ShowConsole() { MainWindow.Show(); MainWindow.WindowState = WindowState.Normal; MainWindow.Activate(); }
        internal void ExitConsole() { Exiting = true; Shutdown(); }
        protected override void OnExit(ExitEventArgs e)
        {
            Exiting = true;
            if (Pet != null) Pet.Dispose();
            if (tray != null) tray.Dispose();
            if (singleInstance != null) singleInstance.Dispose();
            base.OnExit(e);
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
