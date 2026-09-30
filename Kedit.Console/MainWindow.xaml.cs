using System;
using System.IO;
using System.IO.Pipes;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Kedit.Console
{
    public partial class MainWindow : Window
    {
        private readonly CancellationTokenSource pipeCancellation = new CancellationTokenSource();
        private Thread pipeThread;
        private IntPtr ahkWindow = IntPtr.Zero;
        private bool applyingState;

        [StructLayout(LayoutKind.Sequential)]
        private struct CopyDataStruct
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, ref CopyDataStruct lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public MainWindow()
        {
            InitializeComponent();
            StartPipeServer();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void StartPipeServer()
        {
            pipeThread = new Thread(PipeServerLoop) { IsBackground = true };
            pipeThread.Start();
        }

        private void PipeServerLoop()
        {
            while (!pipeCancellation.IsCancellationRequested)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(
                        "Kedit.Console", PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                        PipeOptions.None))
                    {
                        server.WaitForConnection();
                        using (var reader = new StreamReader(server, Encoding.UTF8, false, 1024, true))
                        using (var writer = new StreamWriter(server, new UTF8Encoding(false), 1024, true) { AutoFlush = true })
                        {
                            string request = reader.ReadLine() ?? "";
                            string command = request.IndexOf("get_state", StringComparison.OrdinalIgnoreCase) >= 0
                                ? "get_state" : "unknown";
                            string response = "{\"ok\":true,\"command\":\"" + command + "\",\"version\":\"0.1.0\"}";
                            writer.WriteLine(response);
                            Dispatcher.BeginInvoke(new Action(() => ApplyAhkState(request, command)));
                        }
                    }
                }
                catch
                {
                    if (!pipeCancellation.IsCancellationRequested)
                        Thread.Sleep(200);
                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var args = Environment.GetCommandLineArgs();
            long hwndValue;
            if (args.Length >= 3)
            {
                string hwndText = args[2].Trim();
                if (long.TryParse(hwndText, NumberStyles.Integer, CultureInfo.InvariantCulture, out hwndValue))
                    ahkWindow = new IntPtr(hwndValue);
                else if (hwndText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(hwndText.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hwndValue))
                    ahkWindow = new IntPtr(hwndValue);
            }
            if (!IsWindow(ahkWindow))
                ahkWindow = FindWindow("AutoHotkey", null);
            string videoPath = args.Length >= 2 ? args[1] : FindDefaultVideo();
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
                return;

            try
            {
                BackgroundVideo.Source = new Uri(videoPath, UriKind.Absolute);
                BackgroundVideo.Play();
            }
            catch
            {
                BackgroundVideo.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyAhkState(string request, string command)
        {
            PipeStatus.Text = "已连接 Kedit 主程序 · " + command
                + (ahkWindow == IntPtr.Zero ? "（设置通道未建立）" : "（设置通道已建立）");
            if (command != "get_state")
                return;

            applyingState = true;
            AutoUpdateToggle.IsEnabled = true;
            OsdToggle.IsEnabled = true;
            AutoUpdateToggle.IsChecked = ReadJsonBool(request, "auto_update");
            OsdToggle.IsChecked = ReadJsonBool(request, "osd");
            HotkeyStatus.Text = "快捷键：定义 " + ReadJsonString(request, "go_to_def")
                + " · 书签 " + ReadJsonString(request, "vs_bookmark_toggle")
                + " · 下一个 " + ReadJsonString(request, "vs_bookmark_next")
                + " · 上一个 " + ReadJsonString(request, "vs_bookmark_previous")
                + " · 重做 " + ReadJsonString(request, "vs_redo");
            GoToDefInput.Text = ReadJsonString(request, "go_to_def");
            BookmarkToggleInput.Text = ReadJsonString(request, "vs_bookmark_toggle");
            BookmarkNextInput.Text = ReadJsonString(request, "vs_bookmark_next");
            BookmarkPreviousInput.Text = ReadJsonString(request, "vs_bookmark_previous");
            RedoInput.Text = ReadJsonString(request, "vs_redo");
            applyingState = false;
        }

        private static bool ReadJsonBool(string json, string key)
        {
            var match = System.Text.RegularExpressions.Regex.Match(json,
                "\\\"" + key + "\\\"\\s*:\\s*\\\"?(0|1|true|false)\\\"?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success && (match.Groups[1].Value == "1" ||
                match.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase));
        }

        private static string ReadJsonString(string json, string key)
        {
            var match = System.Text.RegularExpressions.Regex.Match(json,
                "\\\"" + key + "\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : "未读取";
        }

        private void AutoUpdateToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!applyingState && AutoUpdateToggle.IsEnabled)
                SendCommandToAhk("set_auto_update=" + (AutoUpdateToggle.IsChecked == true ? "1" : "0"));
        }

        private void OsdToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!applyingState && OsdToggle.IsEnabled)
                SendCommandToAhk("set_osd=" + (OsdToggle.IsChecked == true ? "1" : "0"));
        }

        private void SaveHotkeys_Click(object sender, RoutedEventArgs e)
        {
            SendCommandToAhk("set_hotkey|GoToDef|" + GoToDefInput.Text.Trim());
            SendCommandToAhk("set_hotkey|VS_BookmarkToggle|" + BookmarkToggleInput.Text.Trim());
            SendCommandToAhk("set_hotkey|VS_BookmarkNext|" + BookmarkNextInput.Text.Trim());
            SendCommandToAhk("set_hotkey|VS_BookmarkPrevious|" + BookmarkPreviousInput.Text.Trim());
            SendCommandToAhk("set_hotkey|VS_Redo|" + RedoInput.Text.Trim());
            PipeStatus.Text = "快捷键设置已发送";
        }

        private void SendCommandToAhk(string command)
        {
            try
            {
                string commandDirectory = Path.Combine(Path.GetTempPath(), "Kedit_Media");
                Directory.CreateDirectory(commandDirectory);
                string commandPath = Path.Combine(commandDirectory, "KeditConsoleCommand.txt");
                string tempPath = commandPath + ".tmp";
                File.WriteAllText(tempPath, command, new UTF8Encoding(false));
                if (File.Exists(commandPath)) File.Delete(commandPath);
                File.Move(tempPath, commandPath);
                PipeStatus.Text = "设置已写入，等待 AHK 应用";
            }
            catch
            {
                PipeStatus.Text = "设置写入失败";
                return;
            }
            if (ahkWindow == IntPtr.Zero)
            {
                return;
            }

            IntPtr data = Marshal.StringToHGlobalUni(command);
            try
            {
                var copy = new CopyDataStruct
                {
                    dwData = IntPtr.Zero,
                    cbData = (command.Length + 1) * 2,
                    lpData = data
                };
                IntPtr result = SendMessage(ahkWindow, 0x4A, IntPtr.Zero, ref copy);
                if (result != IntPtr.Zero)
                    PipeStatus.Text = "已发送设置：" + command;
            }
            finally
            {
                Marshal.FreeHGlobal(data);
            }
        }

        private string FindDefaultVideo()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "side.mp4"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "side.mp4")),
                Path.Combine(Path.GetTempPath(), "Kedit_Media", "side.mp4")
            };
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        private void RootCard_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double radius = 22;
            RootCard.Clip = new RectangleGeometry(
                new Rect(0, 0, RootCard.ActualWidth, RootCard.ActualHeight), radius, radius);
        }

        private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Position = TimeSpan.Zero;
            BackgroundVideo.Play();
        }

        private void BackgroundVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            BackgroundVideo.Visibility = Visibility.Collapsed;
        }

        private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.Button)
                return;
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            pipeCancellation.Cancel();
            BackgroundVideo.Stop();
        }
    }
}
