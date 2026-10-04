using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kedit.Console
{
    // Opt-in local integration check; exercises the real renderer and window lifecycle.
    internal static class PetDiagnostics
    {
        internal static async void Run(App app, MainWindow panel, string directory, bool selfTest)
        {
            try
            {
                Directory.CreateDirectory(directory);
                panel.ShowPetPage();
                for (int i = 0; i < 150 && !app.Pet.IsReady; i++) await Task.Delay(200);
                Check(app.Pet.IsReady, "Live2D did not become ready: " + app.Pet.Status);
                await Task.Delay(3000);
                app.Pet.Capture(Path.Combine(directory, "pet-1.png"));
                await Task.Delay(1100);
                app.Pet.Capture(Path.Combine(directory, "pet-2.png"));
                Capture(panel, Path.Combine(directory, "console.png"));
                if (!selfTest) return;
                Check((app.Pet.WindowStyle & 0x08000000) != 0, "NOACTIVATE is missing");
                app.Pet.SetOptions(true, true);
                Check((app.Pet.WindowStyle & 0x20) != 0, "Click-through was not applied");
                app.Pet.SetOptions(true, false);
                Check((app.Pet.WindowStyle & 0x20) == 0, "Click-through could not be cleared");
                double originalSize = app.Pet.Settings.Size;
                app.Pet.SetSize(480); await Task.Delay(500);
                app.Pet.Capture(Path.Combine(directory, "pet-resized.png"));
                app.Pet.SetSize(originalSize);
                app.Pet.ResetPosition();
                app.Pet.Notify(2); await Task.Delay(500);
                app.Pet.Capture(Path.Combine(directory, "pet-bookmark.png"));
                app.Pet.Notify(6); await Task.Delay(500);
                app.Pet.Capture(Path.Combine(directory, "pet-paused.png"));
                app.Pet.Notify(7); await Task.Delay(1000);
                app.Pet.Capture(Path.Combine(directory, "pet-resumed.png"));
                panel.Close();
                Check(!panel.IsVisible && app.Pet.IsReady, "Closing the console stopped the pet");
                app.ShowConsole();
                Check(panel.IsVisible, "Console could not be reopened");
                app.Pet.SetEnabled(false);
                Check(!app.Pet.HasWindow, "Disabled pet retained its render window");
                app.Pet.SetEnabled(true);
                for (int i = 0; i < 100 && !app.Pet.IsReady; i++) await Task.Delay(200);
                Check(app.Pet.IsReady, "Pet could not be re-enabled");
                File.WriteAllText(Path.Combine(directory, "result.txt"), "PASS: real model rendering, NOACTIVATE, click-through on/off, resize, event cues, close/reopen console, dispose/re-enable pet.");
                app.ExitConsole();
            }
            catch (Exception ex) {
                PetRuntime.Log("Diagnostics failed: " + ex);
                File.WriteAllText(Path.Combine(directory, "result.txt"), "FAIL: " + ex);
                if (selfTest) app.ExitConsole();
            }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Capture(Window window, string path)
        {
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
