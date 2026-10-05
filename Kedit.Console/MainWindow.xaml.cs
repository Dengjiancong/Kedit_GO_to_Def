using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kedit.Console
{
    public partial class MainWindow : Window
    {
        private sealed class ShortcutItem
        {
            public string Category, Key, JsonKey, Title, Description, DefaultKey, VideoFile;
        }

        private readonly ShortcutItem[] shortcuts = {
            new ShortcutItem { Category="kedit", Key="GoToDef", JsonKey="go_to_def", Title="跳转到定义", Description="在 Kedit 中用选定按键跳转到定义。", DefaultKey="XButton1", VideoFile="go_to_definition.mp4" },
            new ShortcutItem { Category="vs", Key="VS_BookmarkToggle", JsonKey="vs_bookmark_toggle", Title="设置或取消书签", Description="在 Visual Studio 中设置或取消当前行书签。", DefaultKey="^F2", VideoFile="vs_bookmark_toggle.mp4" },
            new ShortcutItem { Category="vs", Key="VS_BookmarkNext", JsonKey="vs_bookmark_next", Title="下一个书签", Description="在 Visual Studio 中跳转到下一个书签。", DefaultKey="F2", VideoFile="vs_bookmark_next.mp4" },
            new ShortcutItem { Category="vs", Key="VS_BookmarkPrevious", JsonKey="vs_bookmark_previous", Title="上一个书签", Description="在 Visual Studio 中跳转到上一个书签。", DefaultKey="+F2", VideoFile="vs_bookmark_previous.mp4" },
            new ShortcutItem { Category="vs", Key="VS_Redo", JsonKey="vs_redo", Title="重做", Description="在 Visual Studio 中执行重做。", DefaultKey="^y", VideoFile="vs_redo.mp4" }
        };

        private readonly Dictionary<string, string> currentKeys = new Dictionary<string, string>();
        private readonly CancellationTokenSource pipeCancellation = new CancellationTokenSource();
        private string category = "kedit";
        private ShortcutItem selected;
        private bool applyingState;
        private bool applyingPet;
        private PetController Pet { get { return ((App)Application.Current).Pet; } }

        public MainWindow()
        {
            InitializeComponent();
            foreach (var item in shortcuts) currentKeys[item.Key] = item.DefaultKey;
            ShowCategory("kedit");
            var pipeThread = new Thread(PipeServerLoop) { IsBackground = true };
            pipeThread.Start();
            Closed += MainWindow_Closed;
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) {
                var app = (App)Application.Current;
                if (!app.Exiting && Pet.Settings.Enabled) { e.Cancel = true; Hide(); BackgroundVideo.Pause(); }
                else if (!app.Exiting) Dispatcher.BeginInvoke(new Action(app.ExitConsole));
            };
            IsVisibleChanged += delegate { if (IsVisible && BackgroundVideo.Source != null) BackgroundVideo.Play(); };
            SourceInitialized += delegate {
                System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle).AddHook(ConsoleMessage);
            };
            Pet.Changed += delegate { RefreshPet(); };
            Pet.MetricsChanged += delegate { PetFrameRate.Text = Pet.FrameRateText; PetTypingRate.Text = Pet.TypingRateText; };
            RefreshPet();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var args = Environment.GetCommandLineArgs();
            string videoPath = args.Length >= 2 ? args[1] : Path.Combine(Path.GetTempPath(), "Kedit_Media", "side.mp4");
            if (!File.Exists(videoPath)) return;
            try
            {
                BackgroundVideo.Source = new Uri(videoPath, UriKind.Absolute);
                BackgroundVideo.Play();
            }
            catch { BackgroundVideo.Visibility = Visibility.Collapsed; }
        }

        private void PipeServerLoop()
        {
            while (!pipeCancellation.IsCancellationRequested)
            {
                try
                {
                    using (var server = new NamedPipeServerStream("Kedit.Console", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                    {
                        server.WaitForConnection();
                        using (var reader = new StreamReader(server, Encoding.UTF8, false, 1024, true))
                        using (var writer = new StreamWriter(server, new UTF8Encoding(false), 1024, true) { AutoFlush = true })
                        {
                            string request = reader.ReadLine() ?? "";
                            string command = request.IndexOf("get_state", StringComparison.OrdinalIgnoreCase) >= 0 ? "get_state" : "unknown";
                            writer.WriteLine("{\"ok\":true,\"command\":\"" + command + "\",\"version\":\"0.1.0\"}");
                            Dispatcher.BeginInvoke(new Action(() => ApplyAhkState(request, command)));
                        }
                    }
                }
                catch
                {
                    if (!pipeCancellation.IsCancellationRequested) Thread.Sleep(200);
                }
            }
        }

        private void ApplyAhkState(string request, string command)
        {
            PipeStatus.Text = "已连接 Kedit 主程序 · " + command;
            if (command != "get_state") return;
            applyingState = true;
            AutoUpdateToggle.IsEnabled = true;
            OsdToggle.IsEnabled = true;
            AutoUpdateToggle.IsChecked = ReadJsonBool(request, "auto_update");
            OsdToggle.IsChecked = ReadJsonBool(request, "osd");
            foreach (var item in shortcuts) currentKeys[item.Key] = ReadJsonString(request, item.JsonKey, item.DefaultKey);
            applyingState = false;
            ShowCategory(category);
            if (selected != null) ShowDetail(selected);
        }

        private static bool ReadJsonBool(string json, string key)
        {
            var match = System.Text.RegularExpressions.Regex.Match(json, "\\\"" + key + "\\\"\\s*:\\s*\\\"?(0|1|true|false)\\\"?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success && (match.Groups[1].Value == "1" || match.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase));
        }

        private static string ReadJsonString(string json, string key, string fallback)
        {
            var match = System.Text.RegularExpressions.Regex.Match(json, "\\\"" + key + "\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : fallback;
        }

        private void Category_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            selected = null;
            ShowCategory((string)button.Tag);
        }

        private void ShowCategory(string newCategory)
        {
            category = newCategory;
            DemoVideo.Stop();
            DemoVideo.Source = null;
            DetailPage.Visibility = Visibility.Collapsed;
            SystemPage.Visibility = newCategory == "system" ? Visibility.Visible : Visibility.Collapsed;
            PetPage.Visibility = newCategory == "pet" ? Visibility.Visible : Visibility.Collapsed;
            ListPage.Visibility = newCategory == "system" || newCategory == "pet" ? Visibility.Collapsed : Visibility.Visible;
            PageTitle.Text = newCategory == "kedit" ? "Kedit 快捷键" : newCategory == "vs" ? "Visual Studio" : newCategory == "other" ? "其他快捷键" : newCategory == "pet" ? "Live2D 桌宠" : "通用设置";
            CategoryDescription.Text = newCategory == "other" ? "这一分类对应托盘中的 Kedit 以外快捷键。更多项目将在确认样板布局后接入。" : "选择一个功能，进入演示和快捷键设置。";
            ShortcutList.Children.Clear();
            foreach (var item in shortcuts)
            {
                if (item.Category != newCategory) continue;
                var button = new Button { Tag = item, Width = 330, HorizontalContentAlignment = HorizontalAlignment.Stretch, Style = (Style)FindResource("ShortcutButton") };
                var row = new DockPanel();
                row.Children.Add(new TextBlock { Text = currentKeys[item.Key] + "  ›", Foreground = Brushes.Gold, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Right });
                DockPanel.SetDock(row.Children[0], Dock.Right);
                row.Children.Add(new TextBlock { Text = item.Title, Foreground = Brushes.White, FontSize = 16 });
                button.Content = row;
                button.Click += Shortcut_Click;
                ShortcutList.Children.Add(button);
            }
        }

        private void Shortcut_Click(object sender, RoutedEventArgs e) { ShowDetail((ShortcutItem)((Button)sender).Tag); }

        private void ShowDetail(ShortcutItem item)
        {
            selected = item;
            ListPage.Visibility = Visibility.Collapsed;
            SystemPage.Visibility = Visibility.Collapsed;
            DetailPage.Visibility = Visibility.Visible;
            PageTitle.Text = item.Title;
            DetailTitle.Text = item.Title;
            DetailDescription.Text = item.Description;
            HotkeyInput.Text = currentKeys[item.Key];
            // 当前样板统一使用 update_bg.mp4；后续再按快捷键替换为独立演示视频。
            string path = Path.Combine(Path.GetTempPath(), "Kedit_Media", "update_bg.mp4");
            VideoPathHint.Text = "演示视频：" + path;
            DemoVideo.Stop();
            DemoVideo.Source = null;
            VideoPlaceholder.Visibility = Visibility.Visible;
            if (File.Exists(path))
            {
                DemoVideo.Source = new Uri(path, UriKind.Absolute);
                DemoVideo.Play();
                VideoPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e) { selected = null; ShowCategory(category); }
        private void DemoVideo_MediaEnded(object sender, RoutedEventArgs e) { DemoVideo.Position = TimeSpan.Zero; DemoVideo.Play(); }
        private void DemoVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e) { VideoPlaceholder.Visibility = Visibility.Visible; }
        private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e) { BackgroundVideo.Position = TimeSpan.Zero; BackgroundVideo.Play(); }

        private void HotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            e.Handled = true;
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
            string name = KeyToAhkName(key);
            if (name == null) return;
            string value = "";
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) value += "^";
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) value += "!";
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) value += "+";
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) value += "#";
            HotkeyInput.Text = value + name;
        }

        private void HotkeyInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton1 || e.ChangedButton == MouseButton.XButton2)
            {
                HotkeyInput.Text = e.ChangedButton.ToString();
                e.Handled = true;
            }
        }

        private static string KeyToAhkName(Key key)
        {
            if (key >= Key.F1 && key <= Key.F24) return key.ToString();
            if (key >= Key.A && key <= Key.Z) return key.ToString().ToLowerInvariant();
            if (key >= Key.D0 && key <= Key.D9) return key.ToString().Substring(1);
            if (key == Key.Space) return "Space";
            if (key == Key.Enter) return "Enter";
            if (key == Key.Escape) return "Esc";
            if (key == Key.Tab) return "Tab";
            if (key == Key.Back) return "Backspace";
            if (key == Key.Delete) return "Delete";
            if (key == Key.Insert) return "Insert";
            if (key == Key.Home || key == Key.End || key == Key.PageUp || key == Key.PageDown) return key.ToString();
            return null;
        }

        private void SaveHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (selected == null || string.IsNullOrWhiteSpace(HotkeyInput.Text)) return;
            if (SendCommandToAhk("set_hotkey|" + selected.Key + "|" + HotkeyInput.Text.Trim()))
            {
                currentKeys[selected.Key] = HotkeyInput.Text.Trim();
                PipeStatus.Text = "已提交 " + selected.Title + " 快捷键设置";
            }
        }

        private void RestoreDefault_Click(object sender, RoutedEventArgs e)
        {
            if (selected != null) HotkeyInput.Text = selected.DefaultKey;
        }

        private void AutoUpdateToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!applyingState && AutoUpdateToggle.IsEnabled) SendCommandToAhk("set_auto_update=" + (AutoUpdateToggle.IsChecked == true ? "1" : "0"));
        }

        private void OsdToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!applyingState && OsdToggle.IsEnabled) SendCommandToAhk("set_osd=" + (OsdToggle.IsChecked == true ? "1" : "0"));
        }

        private bool SendCommandToAhk(string command)
        {
            try
            {
                string directory = Path.Combine(Path.GetTempPath(), "Kedit_Media");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "KeditConsoleCommand.txt");
                string temp = path + ".tmp";
                File.WriteAllText(temp, command, new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                PipeStatus.Text = "设置已写入，等待 AHK 应用";
                return true;
            }
            catch (Exception ex)
            {
                PipeStatus.Text = "设置写入失败：" + ex.Message;
                return false;
            }
        }

        private void RootCard_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RootCard.Clip = new RectangleGeometry(new Rect(0, 0, RootCard.ActualWidth, RootCard.ActualHeight), 22, 22);
        }

        private IntPtr ConsoleMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x8001) { Pet.Notify(wParam.ToInt32()); handled = true; }
            if (msg == 0x8002) { ((App)Application.Current).ShowConsole(); handled = true; }
            return IntPtr.Zero;
        }

        private void RefreshPet()
        {
            applyingPet = true;
            PetModelPath.Text = string.IsNullOrEmpty(Pet.Settings.ModelPath) ? "尚未选择模型（.model3.json）" : Pet.Settings.ModelPath;
            PetEnabledToggle.IsChecked = Pet.Settings.Enabled;
            PetTopmostToggle.IsChecked = Pet.Settings.Topmost;
            PetClickThroughToggle.IsChecked = Pet.Settings.ClickThrough;
            PetSizeSlider.Value = Pet.Settings.Size;
            PetSizeText.Text = "画布 " + Pet.Settings.Size.ToString("0");
            PetFrameLimit.SelectedValue = Pet.Settings.FrameLimit.ToString();
            PetFrameRate.Text = Pet.FrameRateText;
            PetMouseFollow.IsChecked = Pet.Settings.MouseFollow;
            PetHeadFollow.IsChecked = Pet.Settings.HeadFollow;
            PetTyping.IsChecked = Pet.Settings.TypingEnabled;
            PetTypingScope.SelectedValue = Pet.Settings.TypingScope;
            PetFollowAmount.Value = Pet.Settings.FollowAmount;
            PetFollowSensitivity.Value = Pet.Settings.FollowSensitivity;
            PetFollowSpeed.Value = Pet.Settings.FollowSpeed;
            PetFollowAmountText.Text = "跟随幅度：" + Pet.Settings.FollowAmount.ToString("0") + "%";
            PetFollowSensitivityText.Text = "灵敏度：" + Pet.Settings.FollowSensitivity.ToString("0.0") + " 倍";
            PetFollowSpeedText.Text = "跟随速度：" + Pet.Settings.FollowSpeed.ToString("0.0") + " 倍";
            PetScrollRate.Value = Pet.Settings.ScrollRate; PetScrollSeconds.Value = Pet.Settings.ScrollSeconds;
            PetScrollRateText.Text = "滚动文字触发速度：" + Pet.Settings.ScrollRate + " 次按键／分钟";
            PetScrollSecondsText.Text = "持续达标时长：" + Pet.Settings.ScrollSeconds + " 秒";
            PetTypingRate.Text = Pet.TypingRateText;
            PetInteractionStatus.Text = Pet.InteractionStatus ?? "开启桌宠后检测模型互动能力。";
            PetStatus.Text = Pet.Status;
            PetCare.IsChecked = Pet.Settings.CareEnabled; PetRest.IsChecked = Pet.Settings.RestEnabled;
            PetBubbleOffset.Value = Pet.Settings.BubbleOffsetPercent; PetMouthAmount.Value = Pet.Settings.MouthAmount;
            PetBubbleOffsetText.Text = "气泡下移：" + Pet.Settings.BubbleOffsetPercent.ToString("0") + "%（随角色缩放）";
            PetMouthAmountText.Text = "说话张合幅度：" + Pet.Settings.MouthAmount.ToString("0") + "%";
            PetSwordSensitivity.Value=Pet.Settings.SwordSensitivity; PetSwordAmount.Value=Pet.Settings.SwordAmount; PetSwordHead.Value=Pet.Settings.SwordHeadAmount;
            PetSwordSensitivityText.Text="挥剑灵敏度："+Pet.Settings.SwordSensitivity.ToString("0.0")+" 倍";
            PetSwordAmountText.Text="挥剑幅度："+Pet.Settings.SwordAmount.ToString("0")+"%";
            PetSwordHeadText.Text="持剑头部幅度：普通跟随的 "+Pet.Settings.SwordHeadAmount.ToString("0")+"%";
            PetSwordHeadSensitivity.Value=Pet.Settings.SwordHeadSensitivity;
            PetSwordHeadSensitivityText.Text="持剑头部灵敏度："+Pet.Settings.SwordHeadSensitivity.ToString("0.0")+" 倍";
            PetSwordHeadSpeed.Value=Pet.Settings.SwordHeadSpeed;
            PetSwordHeadSpeedText.Text="持剑头部跟随速度："+Pet.Settings.SwordHeadSpeed.ToString("0.0")+" 倍";
            PetCareFrequency.SelectedValue = Pet.Settings.CareMinutes.ToString();
            PetQuietStatus.Text = Pet.Settings.QuietUntil > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ?
                "自动关心安静至 " + DateTimeOffset.FromUnixTimeMilliseconds(Pet.Settings.QuietUntil).LocalDateTime.ToString("HH:mm") + "；手动互动可用。" : "自动关心遵循开关和冷却；手动互动始终可用。";
            if (!object.ReferenceEquals(PetResources.ItemsSource, Pet.Resources)) {
                PetResources.ItemsSource = Pet.Resources; if (PetResources.Items.Count > 0) PetResources.SelectedIndex = 0;
            }
            PetCompanionStatus.Text = Pet.CompanionStatus;
            if(PetAutomationHost.Content==null)PetAutomationHost.Content=new PetAutomationPanel(Pet,(Style)FindResource("ActionButton"));
            applyingPet = false;
        }
        internal void ShowPetPage() { selected = null; ShowCategory("pet"); }
        internal void SelectFrameLimitForDiagnostics(int limit) { PetFrameLimit.SelectedIndex = -1; PetFrameLimit.SelectedValue = limit.ToString(); }
        internal void ShowPetInteractionsForDiagnostics() { PetInteractionStatus.BringIntoView(); }
        internal void ShowCompanionForDiagnostics() { PetCompanionStatus.BringIntoView(); }
        internal void ShowPresentationForDiagnostics() { PetMouthAmount.BringIntoView(); }
        internal void ShowSwordForDiagnostics() { PetSwordHeadSpeed.BringIntoView(); }
        internal void ShowAutomationForDiagnostics(bool music) { ((PetAutomationPanel)PetAutomationHost.Content).ShowForDiagnostics(music); }
        internal void OpenPetResourcesForDiagnostics() { PetCompanionStatus.BringIntoView(); PetResources.IsDropDownOpen=true; }
        internal void ClosePetResourcesForDiagnostics() { PetResources.IsDropDownOpen=false; }
        internal FrameworkElement PetResourcesPopupForDiagnostics() { return ((System.Windows.Controls.Primitives.Popup)PetResources.Template.FindName("PART_Popup",PetResources)).Child as FrameworkElement; }
        internal void SetScrollForDiagnostics(int rate, int seconds) { PetScrollRate.Value = rate; PetScrollSeconds.Value = seconds; }
        internal void SetFollowForDiagnostics(double amount, double sensitivity, double speed)
        {
            PetFollowAmount.Value = amount; PetFollowSensitivity.Value = sensitivity; PetFollowSpeed.Value = speed;
        }
        private void ChoosePetModel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog {
                Title = "选择 Live2D 模型入口", Filter = "Live2D 模型 (*.model3.json)|*.model3.json", CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == true) Pet.SelectModel(dialog.FileName);
        }
        private void PetEnabled_Changed(object sender, RoutedEventArgs e) { if (!applyingPet && IsLoaded) Pet.SetEnabled(PetEnabledToggle.IsChecked == true); }
        private void PetOptions_Changed(object sender, RoutedEventArgs e) { if (!applyingPet && IsLoaded) Pet.SetOptions(PetTopmostToggle.IsChecked == true, PetClickThroughToggle.IsChecked == true); }
        private void PetInteractions_Changed(object sender, RoutedEventArgs e) { SavePetInteractions(); }
        private void PetTypingScope_Changed(object sender, SelectionChangedEventArgs e) { SavePetInteractions(); }
        private void PetScroll_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!applyingPet && IsLoaded) Pet.SetScroll((int)PetScrollRate.Value, (int)PetScrollSeconds.Value);
        }
        private void PetFollow_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!applyingPet && IsLoaded) Pet.SetFollow(PetFollowAmount.Value, PetFollowSensitivity.Value, PetFollowSpeed.Value);
        }
        private void PetResetInteractions_Click(object sender, RoutedEventArgs e) { Pet.ResetInteractions(); }
        private void SavePetInteractions()
        {
            if (!applyingPet && IsLoaded) Pet.SetInteractions(PetMouseFollow.IsChecked == true, PetHeadFollow.IsChecked == true,
                PetTyping.IsChecked == true, Convert.ToString(PetTypingScope.SelectedValue));
        }
        private void PetSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (!applyingPet && IsLoaded) Pet.SetSize(e.NewValue); }
        private void PetFrameLimit_Changed(object sender, SelectionChangedEventArgs e)
        {
            int limit;
            if (!applyingPet && IsLoaded && int.TryParse(Convert.ToString(PetFrameLimit.SelectedValue), out limit)) Pet.SetFrameLimit(limit);
        }
        private void PreviewPet_Click(object sender, RoutedEventArgs e) { Pet.Preview(); }
        private void ResetPetPosition_Click(object sender, RoutedEventArgs e) { Pet.ResetPosition(); }
        private void PetResource_Changed(object sender, SelectionChangedEventArgs e)
        {
            var resource = PetResources.SelectedItem as PetResource;
            if (PetResourceDetail != null) PetResourceDetail.Text = resource == null ? "开启桌宠后显示当前模型资源。" : resource.detail;
        }
        private void PetCompanion_Click(object sender, RoutedEventArgs e)
        {
            string action = Convert.ToString(((Button)sender).Tag);
            if (action == "quiet" || action == "unquiet") { Pet.SetQuiet(action == "quiet"); return; }
            if (action == "once" || action == "hold") {
                var resource = PetResources.SelectedItem as PetResource;
                if (resource == null || !resource.available) { Pet.SetCompanionStatus("请先选择可用资源。"); return; }
                Pet.CompanionCommand("select", resource.id, action == "hold");
            } else Pet.CompanionCommand(action);
        }
        private void SavePetCare()
        {
            int minutes;
            if (!applyingPet && IsLoaded && int.TryParse(Convert.ToString(PetCareFrequency.SelectedValue), out minutes))
                Pet.SetCare(PetCare.IsChecked == true, minutes, PetRest.IsChecked == true);
        }
        private void PetPresentation_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!applyingPet && IsLoaded) Pet.SetPresentation(PetBubbleOffset.Value, PetMouthAmount.Value);
        }
        private void PetSword_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(!applyingPet && IsLoaded)Pet.SetSword(PetSwordSensitivity.Value,PetSwordAmount.Value,PetSwordHead.Value,PetSwordHeadSensitivity.Value,PetSwordHeadSpeed.Value);
        }
        private void PetCare_Changed(object sender, RoutedEventArgs e) { SavePetCare(); }
        private void PetCareFrequency_Changed(object sender, SelectionChangedEventArgs e) { SavePetCare(); }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(e.OriginalSource is Button) && e.ButtonState == MouseButtonState.Pressed) DragMove();
        }
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { Close(); }
        private void MainWindow_Closed(object sender, EventArgs e) { pipeCancellation.Cancel(); DemoVideo.Stop(); }
    }
}
