using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Kedit.Console
{
    internal sealed class PetWindow : Window
    {
        private readonly PetController controller;
        private readonly PetModel model;
        private readonly WebView2CompositionControl browser;
        private readonly Border bubble;
        private readonly TextBlock message;
        private readonly DispatcherTimer bubbleTimer;
        private readonly DispatcherTimer loadTimer;
        private bool closed, ready, dragging;
        private Point dragPoint;
        private double dragLeft, dragTop;
        internal bool IsReady { get { return ready; } }
        internal long ExtendedStyle { get { return GetWindowLong(new WindowInteropHelper(this).Handle, -20).ToInt64(); } }

        public PetWindow(PetController owner, PetModel selected)
        {
            controller = owner;
            model = selected;
            Title = "Kedit Live2D 桌宠";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            var grid = new Grid();
            browser = new WebView2CompositionControl {
                DefaultBackgroundColor = System.Drawing.Color.Transparent,
                IsHitTestVisible = false, Focusable = false, Margin = new Thickness(0, 65, 0, 0)
            };
            grid.Children.Add(browser);
            message = new TextBlock { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
            bubble = new Border {
                Background = new SolidColorBrush(Color.FromArgb(235, 24, 42, 57)), CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Top, Child = message, Visibility = Visibility.Collapsed
            };
            grid.Children.Add(bubble);
            // A near-transparent hit surface supports dragging without browser focus.
            var surface = new Border { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
            surface.MouseLeftButtonDown += BeginDrag;
            surface.MouseMove += MoveDrag;
            surface.MouseLeftButtonUp += EndDrag;
            surface.LostMouseCapture += delegate { dragging = false; };
            surface.MouseWheel += delegate(object sender, MouseWheelEventArgs e) {
                controller.SetSize(controller.Settings.Size + (e.Delta > 0 ? 30 : -30)); e.Handled = true;
            };
            grid.Children.Add(surface);
            Content = grid;
            bubbleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            bubbleTimer.Tick += delegate { bubble.Visibility = Visibility.Collapsed; bubbleTimer.Stop(); };
            loadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            loadTimer.Tick += delegate { loadTimer.Stop(); Fail("模型加载超时，请检查模型资源或重新打开桌宠。"); };
            SourceInitialized += delegate {
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(WindowMessage);
                ApplyOptions();
            };
            Loaded += async delegate { await InitializeBrowser(); };
            Closed += delegate {
                closed = true; loadTimer.Stop(); bubbleTimer.Stop(); browser.Dispose();
            };
            ApplySize();
            if (owner.Settings.HasPosition) { Left = owner.Settings.Left; Top = owner.Settings.Top; EnsureVisible(); }
            else ResetPosition();
        }

        private async Task InitializeBrowser()
        {
            try
            {
                loadTimer.Start();
                string profile = Path.Combine(PetRuntime.DataDirectory, "WebView2");
                var environment = await CoreWebView2Environment.CreateAsync(null, profile);
                if (closed) return;
                await browser.EnsureCoreWebView2Async(environment);
                if (closed) return;
                var core = browser.CoreWebView2;
                core.IsMuted = true;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.Settings.IsPasswordAutosaveEnabled = false;
                core.SetVirtualHostNameToFolderMapping("pet.kedit.local", Path.Combine(PetRuntime.AssetDirectory, "PetWeb"), CoreWebView2HostResourceAccessKind.DenyCors);
                core.SetVirtualHostNameToFolderMapping("model.kedit.local", model.DirectoryName, CoreWebView2HostResourceAccessKind.Allow);
                core.NavigationStarting += delegate(object sender, CoreWebView2NavigationStartingEventArgs e) {
                    Uri uri;
                    if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out uri) || uri.Scheme != "https" || uri.Host != "pet.kedit.local") e.Cancel = true;
                };
                core.NewWindowRequested += delegate(object sender, CoreWebView2NewWindowRequestedEventArgs e) { e.Handled = true; };
                core.PermissionRequested += delegate(object sender, CoreWebView2PermissionRequestedEventArgs e) { e.State = CoreWebView2PermissionState.Deny; };
                core.DownloadStarting += delegate(object sender, CoreWebView2DownloadStartingEventArgs e) { e.Cancel = true; };
                core.ProcessFailed += delegate { Fail("桌宠渲染进程已停止，请关闭后重新打开。"); };
                core.NavigationCompleted += delegate(object sender, CoreWebView2NavigationCompletedEventArgs e) {
                    if (!e.IsSuccess) Fail("桌宠页面加载失败：" + e.WebErrorStatus);
                };
                core.WebMessageReceived += ReceiveMessage;
                string url = "https://model.kedit.local/" + Uri.EscapeDataString(model.FileName);
                browser.Source = new Uri("https://pet.kedit.local/index.html?model=" + Uri.EscapeDataString(url));
            }
            catch (WebView2RuntimeNotFoundException) { Fail("缺少 Microsoft Edge WebView2 Runtime，请安装后重试。"); }
            catch (Exception ex) { if (!closed) Fail(ex.Message); }
        }

        private void ReceiveMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (closed || !e.Source.StartsWith("https://pet.kedit.local/", StringComparison.Ordinal)) return;
            try
            {
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(e.WebMessageAsJson);
                if (data["type"] == "ready") {
                    ready = true; loadTimer.Stop();
                    controller.SetStatus("模型已加载 · " + model.ResourceCount + " 个资源 · 30 FPS");
                    ShowBubble("你好！拖动可移动，滚轮可缩放。");
                }
                else if (data["type"] == "error") Fail(data["text"]);
            }
            catch (Exception ex) { Fail("渲染消息错误：" + ex.Message); }
        }

        private void Fail(string text)
        {
            if (closed) return;
            ready = false; loadTimer.Stop();
            controller.SetStatus("桌宠加载/运行失败：" + text);
            message.Text = "桌宠出现问题，请在中控的桌宠页查看。";
            bubble.Visibility = Visibility.Visible;
            bubbleTimer.Stop();
        }

        public void Cue(string text, string kind)
        {
            if (!ready || closed) return;
            ShowBubble(text);
            browser.CoreWebView2.PostWebMessageAsJson(new JavaScriptSerializer().Serialize(new { type = "cue", kind = kind }));
            PetRuntime.Log("Cue: " + kind + " " + text);
        }

        private void ShowBubble(string text) { message.Text = text; bubble.Visibility = Visibility.Visible; bubbleTimer.Stop(); bubbleTimer.Start(); }

        internal void Capture(string path)
        {
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }

        public void ApplyOptions()
        {
            Topmost = controller.Settings.Topmost;
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            long style = GetWindowLong(handle, -20).ToInt64() | 0x08000000L | 0x80L; // NOACTIVATE / TOOLWINDOW
            style = controller.Settings.ClickThrough ? style | 0x20L : style & ~0x20L;
            SetWindowLong(handle, -20, new IntPtr(style));
        }

        public void ApplySize() { Width = controller.Settings.Size; Height = controller.Settings.Size * 1.2 + 65; EnsureVisible(); }

        public void ResetPosition()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 24; Top = area.Bottom - Height - 12;
            SavePosition();
        }

        private void EnsureVisible()
        {
            if (double.IsNaN(Left) || double.IsNaN(Top)) return;
            var bounds = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            Left = Math.Max(bounds.Left, Math.Min(Left, bounds.Right - Width));
            Top = Math.Max(bounds.Top, Math.Min(Top, bounds.Bottom - Height));
        }

        private void BeginDrag(object sender, MouseButtonEventArgs e)
        {
            dragging = true; dragPoint = PointToScreen(e.GetPosition(this)); dragLeft = Left; dragTop = Top;
            ((UIElement)sender).CaptureMouse(); e.Handled = true;
        }
        private void MoveDrag(object sender, MouseEventArgs e)
        {
            if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
            Point point = PointToScreen(e.GetPosition(this));
            var source = PresentationSource.FromVisual(this);
            var delta = source.CompositionTarget.TransformFromDevice.Transform(point - dragPoint);
            Left = dragLeft + delta.X; Top = dragTop + delta.Y;
        }
        private void EndDrag(object sender, MouseButtonEventArgs e)
        {
            if (!dragging) return;
            dragging = false; ((UIElement)sender).ReleaseMouseCapture(); EnsureVisible(); SavePosition(); e.Handled = true;
        }
        private void SavePosition() { controller.Settings.Left = Left; controller.Settings.Top = Top; controller.Settings.HasPosition = true; controller.Save(); }

        private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x21) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
            return IntPtr.Zero;
        }
        private static IntPtr GetWindowLong(IntPtr hwnd, int index) { return IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index) : new IntPtr(GetWindowLong32(hwnd, index)); }
        private static void SetWindowLong(IntPtr hwnd, int index, IntPtr value) { if (IntPtr.Size == 8) SetWindowLongPtr64(hwnd, index, value); else SetWindowLong32(hwnd, index, value.ToInt32()); }
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);
    }
}
