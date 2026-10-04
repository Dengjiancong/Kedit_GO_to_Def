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
        private readonly DispatcherTimer metricsTimer;
        private readonly PetInput input;
        private DateTime lastMetrics;
        private double canvasAspect = 1.2;
        private bool closed, ready, dragging, typingAvailable;
        private Point dragPoint;
        private double dragLeft, dragTop;
        internal bool IsReady { get { return ready; } }
        internal long ExtendedStyle { get { return GetWindowLong(new WindowInteropHelper(this).Handle, -20).ToInt64(); } }

        public PetWindow(PetController owner, PetModel selected)
        {
            controller = owner;
            model = selected;
            input = new PetInput(this, owner.Settings, delegate(double x, double y, int presses) {
                if (ready && !closed) PostInteraction(new { type = "input", x = x, y = y, presses = presses, sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
            });
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
                Focusable = false, Margin = new Thickness(0, 65, 0, 0)
            };
            grid.Children.Add(browser);
            message = new TextBlock { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
            bubble = new Border {
                Background = new SolidColorBrush(Color.FromArgb(235, 24, 42, 57)), CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Top, Child = message, Visibility = Visibility.Collapsed
            };
            grid.Children.Add(bubble);
            // Do not paint a rectangular hit surface: alpha-zero pixels of a
            // layered window naturally pass through to the application below.
            // Intercept body input before WebView2 so dragging cannot focus it.
            grid.PreviewMouseLeftButtonDown += BeginDrag;
            grid.PreviewMouseMove += MoveDrag;
            grid.PreviewMouseLeftButtonUp += EndDrag;
            grid.LostMouseCapture += delegate { dragging = false; };
            grid.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e) {
                controller.SetSize(controller.Settings.Size + (e.Delta > 0 ? 30 : -30)); e.Handled = true;
            };
            Content = grid;
            bubbleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            bubbleTimer.Tick += delegate { bubble.Visibility = Visibility.Collapsed; bubbleTimer.Stop(); };
            loadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            loadTimer.Tick += delegate { loadTimer.Stop(); Fail("模型加载超时，请检查模型资源或重新打开桌宠。"); };
            metricsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            metricsTimer.Tick += delegate {
                if ((DateTime.UtcNow - lastMetrics).TotalSeconds > 4 && controller.RenderFps.HasValue) controller.SetRenderFps(null);
                if ((DateTime.UtcNow - lastMetrics).TotalSeconds > 4) controller.SetTypingMetrics(null, 0, false);
            };
            SourceInitialized += delegate {
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(WindowMessage);
                ApplyOptions();
            };
            Loaded += async delegate { await InitializeBrowser(); };
            Closed += delegate {
                closed = true; input.Dispose(); loadTimer.Stop(); bubbleTimer.Stop(); metricsTimer.Stop(); browser.Dispose();
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
                browser.Source = new Uri("https://pet.kedit.local/index.html?model=" + Uri.EscapeDataString(url) + "&fps=" + controller.Settings.FrameLimit +
                    (Array.IndexOf(Environment.GetCommandLineArgs(), "--self-test-pet") >= 0 ? "&diagnostics=1" : ""));
            }
            catch (WebView2RuntimeNotFoundException) { Fail("缺少 Microsoft Edge WebView2 Runtime，请安装后重试。"); }
            catch (Exception ex) { if (!closed) Fail(ex.Message); }
        }

        private void ReceiveMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (closed || !e.Source.StartsWith("https://pet.kedit.local/", StringComparison.Ordinal)) return;
            try
            {
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(e.WebMessageAsJson);
                string type = Convert.ToString(data["type"]);
                if (type == "ready") {
                    double width = Convert.ToDouble(data["canvasWidth"]), height = Convert.ToDouble(data["canvasHeight"]);
                    if (width <= 0 || height <= 0 || double.IsNaN(width + height) || double.IsInfinity(width + height))
                        throw new InvalidDataException("模型原始画布尺寸无效");
                    canvasAspect = height / width;
                    ApplySize();
                    ready = true; loadTimer.Stop();
                    ApplyFrameLimit(); ApplyInteractions(); metricsTimer.Start();
                    controller.SetStatus("模型已加载 · " + model.ResourceCount + " 个资源 · 原始画布");
                    ShowBubble("你好！拖动可移动，滚轮可缩放。");
                }
                else if (type == "interactionCapabilities") {
                    typingAvailable = Convert.ToBoolean(data["typing"]);
                    controller.SetInteractionStatus((Convert.ToBoolean(data["eyes"]) ? "支持眼球跟随" :
                        Convert.ToBoolean(data["head"]) ? "模型无独立眼球参数，使用轻微转头跟随" : "模型不支持视线或转头跟随") +
                        " · " + (Convert.ToBoolean(data["typing"]) ? "支持敲键盘动作" : "模型敲键盘动作不可用") +
                        (Convert.ToString(data["warning"]) == "" ? "" : "：" + Convert.ToString(data["warning"])));
                }
                else if (type == "interactionWarning") {
                    typingAvailable = false; input.Configure(false);
                    controller.SetInteractionStatus(Convert.ToString(data["text"]));
                }
                else if (type == "typingMetrics" && ready) {
                    double rate = Convert.ToDouble(data["rate"]), seconds = Convert.ToDouble(data["seconds"]);
                    if (Convert.ToInt32(data["scrollRate"]) != controller.Settings.ScrollRate || Convert.ToInt32(data["scrollSeconds"]) != controller.Settings.ScrollSeconds ||
                        double.IsNaN(rate + seconds) || double.IsInfinity(rate + seconds) || rate < 0 || seconds < 0) return;
                    controller.SetTypingMetrics(rate, seconds, Convert.ToBoolean(data["scrolling"]));
                }
                else if (type == "fps" && ready) {
                    double fps = Convert.ToDouble(data["fps"]);
                    if (Convert.ToInt32(data["limit"]) != controller.Settings.FrameLimit || double.IsNaN(fps) || double.IsInfinity(fps) || fps < 0) return;
                    lastMetrics = DateTime.UtcNow;
                    controller.SetRenderFps(fps);
                }
                else if (type == "error") Fail(Convert.ToString(data["text"]));
            }
            catch (Exception ex) { Fail("渲染消息错误：" + ex.Message); }
        }

        private void Fail(string text)
        {
            if (closed) return;
            ready = false; input.Dispose(); loadTimer.Stop(); metricsTimer.Stop(); controller.SetRenderFps(null);
            controller.SetTypingMetrics(null, 0, false);
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

        public void ApplyFrameLimit()
        {
            lastMetrics = DateTime.UtcNow;
            controller.SetRenderFps(null);
            if (!ready || closed) return;
            browser.CoreWebView2.PostWebMessageAsJson(new JavaScriptSerializer().Serialize(new { type = "settings", frameLimit = controller.Settings.FrameLimit }));
        }

        private void PostInteraction(object data) { browser.CoreWebView2.PostWebMessageAsJson(new JavaScriptSerializer().Serialize(data)); }
        public void ApplyInteractions(bool configureInput = true)
        {
            if (!ready || closed) return;
            PostInteraction(new { type = "interactionSettings", mouseFollow = controller.Settings.MouseFollow,
                headFollow = controller.Settings.HeadFollow, typingEnabled = controller.Settings.TypingEnabled,
                typingScope = controller.Settings.TypingScope, followAmount = controller.Settings.FollowAmount,
                followSensitivity = controller.Settings.FollowSensitivity, followSpeed = controller.Settings.FollowSpeed,
                scrollRate = controller.Settings.ScrollRate, scrollSeconds = controller.Settings.ScrollSeconds });
            if (configureInput) input.Configure(typingAvailable);
            const string hookWarning = " · 键盘活动监听启动失败，可关闭再开启桌宠重试";
            if (configureInput) {
                string status = (controller.InteractionStatus ?? "").Replace(hookWarning, "");
                if (controller.Settings.TypingEnabled && typingAvailable && !input.HookInstalled) status += hookWarning;
                controller.SetInteractionStatus(status);
            }
        }
        internal Task<string> EvaluateForDiagnostics(string script) { return browser.CoreWebView2.ExecuteScriptAsync(script); }
        internal bool InputHookInstalled { get { return input.HookInstalled; } }
        internal void StopInputForDiagnostics() { input.Dispose(); }

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

        public void ApplySize()
        {
            var source = PresentationSource.FromVisual(this);
            var transform = source == null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
            double maxWidth = screen.Width * transform.M11;
            double maxHeight = Math.Max(100, screen.Height * transform.M22 - 65);
            Width = Math.Max(1, Math.Min(controller.Settings.Size, Math.Min(maxWidth, maxHeight / canvasAspect)));
            Height = Width * canvasAspect + 65;
            EnsureVisible();
        }

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
