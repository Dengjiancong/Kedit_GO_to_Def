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
        private Point middlePoint;
        private bool middlePressed;
        private DateTime lastGestureMove;
        private ContextMenu actionMenu;
        private int menuRequest;
        private DateTime menuRequestedAt;
        private Point menuPoint;
        internal bool IsReady { get { return ready; } }
        internal bool IsDraggingForInput { get { return dragging; } }
        internal double BubbleTopForDiagnostics { get { return bubble.Margin.Top; } }
        internal long ExtendedStyle { get { return GetWindowLong(new WindowInteropHelper(this).Handle, -20).ToInt64(); } }

        public PetWindow(PetController owner, PetModel selected)
        {
            controller = owner;
            model = selected;
            input = new PetInput(this, owner.Settings, delegate(double x, double y, int presses) {
                if(presses>0)controller.AutomationActivity();
                if (ready && !closed) PostInteraction(new { type = "input", x = x, y = y, presses = presses, mouseVX = input.MouseVX, mouseVY = input.MouseVY, sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
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
            grid.PreviewMouseDown += MiddleDown;
            grid.PreviewMouseUp += MiddleUp;
            grid.PreviewMouseRightButtonUp += RequestActionMenu;
            grid.PreviewMouseMove += delegate(object sender, MouseEventArgs e) {
                if(middlePressed && (DateTime.UtcNow-lastGestureMove).TotalMilliseconds>=33) {
                    lastGestureMove=DateTime.UtcNow;SendGesture("move",e.GetPosition(this));
                }
            };
            grid.LostMouseCapture += delegate { CancelGesture(); };
            grid.LostMouseCapture += delegate { dragging = false; };
            grid.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e) {
                controller.SetSize(controller.Settings.Size + (e.Delta > 0 ? 30 : -30)); e.Handled = true;
            };
            Content = grid;
            bubbleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            bubbleTimer.Tick += delegate { bubble.Visibility = Visibility.Collapsed; bubbleTimer.Stop(); if (ready && !closed) PostInteraction(new { type = "speech", duration = 0 }); };
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
                closed = true; if(actionMenu!=null)actionMenu.IsOpen=false; input.Dispose(); loadTimer.Stop(); bubbleTimer.Stop(); metricsTimer.Stop(); browser.Dispose();
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
                    ApplyFrameLimit(); ApplyInteractions(); ApplyCompanion(); metricsTimer.Start();controller.AutomationReady();
                    controller.SetStatus("模型已加载 · " + model.ResourceCount + " 个资源 · 原始画布");
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
                else if (type == "catalog") {
                    var serializer = new JavaScriptSerializer();
                    controller.SetResources(serializer.Deserialize<PetResource[]>(serializer.Serialize(data["items"])));
                }
                else if(type=="wardrobePrepared" || type=="wardrobeManual")controller.AutomationMessage(data);
                else if (type == "companionStatus") controller.SetCompanionStatus(Convert.ToString(data["text"]));
                else if(type=="actionMenu" && ready && Convert.ToInt32(data["request"])==menuRequest &&
                    !controller.Settings.ClickThrough && (DateTime.UtcNow-menuRequestedAt).TotalSeconds<1) {
                    OpenActionMenu(Convert.ToString(data["selected"]));
                }
                else if (type == "bubble" && ready) ShowBubble(Convert.ToString(data["text"]), Convert.ToInt32(data["duration"]));
                else if (type == "combination" && ready) {
                    var serializer = new JavaScriptSerializer();
                    controller.SaveCombination(serializer.Deserialize<string[]>(serializer.Serialize(data["ids"])));
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
        internal void SendAutomation(object data) { if(ready&&!closed)PostInteraction(data); }
        internal void AutomationBubble(string text) { if(ready&&!closed)ShowBubble(text,7000); }
        public void CompanionCommand(string action, string id, bool hold)
        {
            if (action == "reset") { bubble.Visibility = Visibility.Collapsed; bubbleTimer.Stop(); }
            if (ready && !closed) PostInteraction(new { type = "companion", action = action, id = id, hold = hold, ids = controller.Settings.FavoriteCombination });
        }
        public void ApplyCompanion()
        {
            PositionBubble();
            if (!ready || closed) return;
            PostInteraction(new { type = "companionSettings", careEnabled = controller.Settings.CareEnabled,
                careMinutes = controller.Settings.CareMinutes, restEnabled = controller.Settings.RestEnabled, quietUntil = controller.Settings.QuietUntil,
                mouthAmount = controller.Settings.MouthAmount, swordSensitivity=controller.Settings.SwordSensitivity,
                swordAmount=controller.Settings.SwordAmount, swordHeadAmount=controller.Settings.SwordHeadAmount,
                swordHeadSensitivity=controller.Settings.SwordHeadSensitivity, swordHeadSpeed=controller.Settings.SwordHeadSpeed });
        }
        private void PositionBubble()
        {
            double available = Math.Max(0, Height - Math.Max(65, bubble.ActualHeight) - 8);
            double offset = Math.Min(available, Math.Max(0, Height-65)*controller.Settings.BubbleOffsetPercent/100);
            bubble.Margin = new Thickness(12, offset, 12, 0);
        }
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

        private void ShowBubble(string text, int duration = 3500)
        {
            duration = Math.Max(1500, Math.Min(10000, duration));
            message.Text = text; bubble.Visibility = Visibility.Visible; bubbleTimer.Stop();
            PositionBubble();
            bubbleTimer.Interval = TimeSpan.FromMilliseconds(duration); bubbleTimer.Start();
            if (ready && !closed) PostInteraction(new { type = "speech", duration = duration });
        }

        internal void Capture(string path, bool neutralBackground = false)
        {
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
            if (neutralBackground) {
                var background = new DrawingVisual();
                using (var drawing = background.RenderOpen()) drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(180,190,200)), null, new Rect(0,0,ActualWidth,ActualHeight));
                bitmap.Render(background);
            }
            bitmap.Render(this);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }

        public void ApplyOptions()
        {
            menuRequest++; if(actionMenu!=null)actionMenu.IsOpen=false;
            CancelGesture();
            if (Mouse.Captured != null && IsAncestorOf(Mouse.Captured as DependencyObject)) Mouse.Capture(null);
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
            PositionBubble();
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
        private void MiddleDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            e.Handled = true;
            if (!ready || controller.Settings.ClickThrough || e.GetPosition(this).Y < 65) return;
            Point bubblePoint = e.GetPosition(bubble);
            if (bubble.IsVisible && bubblePoint.X >= 0 && bubblePoint.X <= bubble.ActualWidth && bubblePoint.Y >= 0 && bubblePoint.Y <= bubble.ActualHeight) return;
            middlePoint = e.GetPosition(this);
            middlePressed = true; ((UIElement)sender).CaptureMouse();SendGesture("down",middlePoint);
        }
        private void RequestActionMenu(object sender, MouseButtonEventArgs e)
        {
            e.Handled=true;
            if(!ready || closed || controller.Settings.ClickThrough)return;
            Point point=e.GetPosition(this), inBubble=e.GetPosition(bubble);
            if(point.Y<65 || (bubble.IsVisible && inBubble.X>=0 && inBubble.X<=bubble.ActualWidth && inBubble.Y>=0 && inBubble.Y<=bubble.ActualHeight))return;
            menuPoint=point; menuRequestedAt=DateTime.UtcNow;
            PostInteraction(new {type="requestActionMenu",request=++menuRequest,x=point.X/ActualWidth,y=(point.Y-65)/(ActualHeight-65)});
        }
        private void OpenActionMenu(string selected)
        {
            if(actionMenu!=null)actionMenu.IsOpen=false;
            actionMenu=new ContextMenu { PlacementTarget=this, Placement=System.Windows.Controls.Primitives.PlacementMode.RelativePoint,
                HorizontalOffset=menuPoint.X, VerticalOffset=menuPoint.Y, MinWidth=180 };
            string[] labels={"自然状态（恢复自动联动）","拿水壶","拿剑","打键盘（保持）","打碟（保持）"};
            string[] ids={"","hand-pot","motion-weapon","motion-keyboard","motion-music"};
            for(int i=0;i<labels.Length;i++) {
                string id=ids[i];
                bool available=id=="" || Array.Exists(controller.Resources ?? new PetResource[0],r=>r.id==id&&r.available);
                var item=new MenuItem {Header=labels[i],IsCheckable=true,IsChecked=selected==id,IsEnabled=available};
                item.Click+=delegate {
                    if(id=="")controller.CompanionCommand("reset");
                    else if(id=="hand-pot")controller.CompanionCommand("drink");
                    else if(id=="motion-weapon")controller.CompanionCommand("weapon");
                    else controller.CompanionCommand("select",id,true);
                };
                actionMenu.Items.Add(item);
            }
            actionMenu.Opened+=delegate {
                var source=PresentationSource.FromVisual(actionMenu) as HwndSource;
                if(source!=null) { SetWindowLong(source.Handle,-20,new IntPtr(GetWindowLong(source.Handle,-20).ToInt64()|0x08000000L)); source.AddHook(WindowMessage); }
            };
            var reminder=controller.Automation.Occurrences.FindLast(o=>o.Due.Date==DateTime.Now.Date&&(o.Status=="pending"||o.Status=="snoozed"||o.Status=="executed"));
            if(reminder!=null) {
                actionMenu.Items.Add(new Separator());
                var later=new MenuItem{Header="本次提醒：稍后提醒"};later.Click+=delegate{controller.RespondReminder(reminder.ReminderId,true);};actionMenu.Items.Add(later);
                var ignore=new MenuItem{Header="本次提醒：今天忽略"};ignore.Click+=delegate{controller.RespondReminder(reminder.ReminderId,false);};actionMenu.Items.Add(ignore);
            }
            actionMenu.IsOpen=true;
        }
        internal void OpenActionMenuForDiagnostics(string selected) { menuPoint=new Point(Width*.5,Height*.6);OpenActionMenu(selected); }
        internal ContextMenu ActionMenuForDiagnostics { get { return actionMenu; } }
        private void MiddleUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            e.Handled = true;
            if(middlePressed)SendGesture("up",e.GetPosition(this));
            middlePressed = false; ((UIElement)sender).ReleaseMouseCapture();
        }
        private void SendGesture(string phase,Point point) {
            if(ready&&!closed)PostInteraction(new {type="gesture",phase=phase,x=point.X/ActualWidth,y=(point.Y-65)/(ActualHeight-65),px=point.X,py=point.Y});
        }
        private void CancelGesture() { if(middlePressed)SendGesture("cancel",middlePoint);middlePressed=false; }
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
