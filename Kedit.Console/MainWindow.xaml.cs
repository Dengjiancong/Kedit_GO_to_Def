using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Kedit.Console
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var args = Environment.GetCommandLineArgs();
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
            BackgroundVideo.Stop();
        }
    }
}
