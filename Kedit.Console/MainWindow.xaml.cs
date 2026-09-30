using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

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
            if (args.Length < 2 || !File.Exists(args[1]))
                return;

            try
            {
                BackgroundVideo.Source = new Uri(args[1], UriKind.Absolute);
                BackgroundVideo.Play();
            }
            catch
            {
                BackgroundVideo.Visibility = Visibility.Collapsed;
            }
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
