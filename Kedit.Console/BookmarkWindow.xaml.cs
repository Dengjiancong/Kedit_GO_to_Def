using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Kedit.Console
{
    public partial class BookmarkWindow : Window
    {
        public BookmarkWindow(string toggle, string next, string previous)
        {
            InitializeComponent();
            ToggleInput.Text = toggle;
            NextInput.Text = next;
            PreviousInput.Text = previous;
            string video = Path.Combine(Path.GetTempPath(), "Kedit_Media", "shortcuts", "vs_bookmark.mp4");
            if (File.Exists(video))
            {
                BookmarkVideo.Source = new Uri(video, UriKind.Absolute);
                BookmarkVideo.Play();
            }
        }

        private void HotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl || e.Key == Key.LeftAlt || e.Key == Key.RightAlt
                || e.Key == Key.LeftShift || e.Key == Key.RightShift || e.Key == Key.LWin || e.Key == Key.RWin)
                return;
            string value = "";
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) value += "^";
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) value += "!";
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) value += "+";
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) value += "#";
            value += KeyToAhkName(e.Key);
            var box = sender as System.Windows.Controls.TextBox;
            if (box != null) box.Text = value;
            e.Handled = true;
        }

        private static string KeyToAhkName(Key key)
        {
            if (key >= Key.F1 && key <= Key.F24) return key.ToString();
            if (key >= Key.A && key <= Key.Z) return key.ToString().ToLowerInvariant();
            if (key >= Key.D0 && key <= Key.D9) return key.ToString().Substring(1);
            if (key == Key.Space) return "Space";
            if (key == Key.Enter) return "Enter";
            if (key == Key.Escape) return "Esc";
            return key.ToString();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string[] commands = {
                "set_hotkey|VS_BookmarkToggle|" + ToggleInput.Text.Trim(),
                "set_hotkey|VS_BookmarkNext|" + NextInput.Text.Trim(),
                "set_hotkey|VS_BookmarkPrevious|" + PreviousInput.Text.Trim()
            };
            string directory = Path.Combine(Path.GetTempPath(), "Kedit_Media");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "KeditConsoleCommand.txt");
            File.WriteAllText(path + ".tmp", string.Join("\n", commands), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".tmp", path);
            DialogResult = true;
        }

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            ToggleInput.Text = "^F2";
            NextInput.Text = "F2";
            PreviousInput.Text = "+F2";
        }

        private void Close_Click(object sender, RoutedEventArgs e) { Close(); }
        private void BookmarkVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            BookmarkVideo.Position = TimeSpan.Zero;
            BookmarkVideo.Play();
        }
    }
}
