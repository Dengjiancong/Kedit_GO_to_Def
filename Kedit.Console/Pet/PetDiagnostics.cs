using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
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
                CheckTransparency(app.Pet.DiagnosticWindow);
                await CheckInteractions(app, directory);
                panel.ShowPetInteractionsForDiagnostics();
                await Task.Delay(200);
                Capture(panel, Path.Combine(directory, "console-interactions.png"));
                // Benchmark before any motion cues, keeping the workload idle
                // and the model/window size identical for every FPS preset.
                await Task.Delay(5000);
                await Benchmark(app, panel, directory);
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
                Check(app.Pet.Settings.FrameLimit == 30, "Benchmark did not restore default limit");
                File.WriteAllText(Path.Combine(directory, "result.txt"), "PASS: mouse/head following, looping typing and recovery, pause/resume priority, hook installation/removal, original-canvas rendering, alpha-zero blank hit-through, body hit target, NOACTIVATE, click-through on/off, resize, event cues, 30/60/90/120 UI settings and real FPS reports, close/reopen console, dispose/re-enable pet. Typing activity was supplied by the diagnostic driver, not physical keyboard input. See fps-benchmark.csv for measured results.");
                app.ExitConsole();
            }
            catch (Exception ex) {
                PetRuntime.Log("Diagnostics failed: " + ex);
                File.WriteAllText(Path.Combine(directory, "result.txt"), "FAIL: " + ex);
                if (selfTest) app.ExitConsole();
            }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        private static async Task CheckInteractions(App app, string directory)
        {
            var window = app.Pet.DiagnosticWindow;
            app.Pet.SetInteractions(true, false, true, "editors");
            Check(window.InputHookInstalled, "Keyboard activity hook was not installed");
            window.StopInputForDiagnostics();
            Check(!window.InputHookInstalled, "Keyboard activity hook was not released");
            Check(PetInput.IsTypingKey(0x41) && PetInput.IsTypingKey(0x20) && !PetInput.IsTypingKey(0x70) && !PetInput.IsTypingKey(0x11), "Keyboard classification failed");
            await window.EvaluateForDiagnostics(@"(() => {
                const d = window.petDiagnostics; d.input = {x:1,y:.5,typing:false};
                d.baseline = d.model.internalModel.coreModel.getParameterValueById('ParamExpression7');
                d.model.internalModel.on('beforeModelUpdate', () => {
                    d.headX = d.model.internalModel.coreModel.getParameterValueById('ParamAngleX');
                    d.keyboard = d.model.internalModel.coreModel.getParameterValueById('ParamExpression7');
                });
                d.timer = setInterval(() => d.interactions.receive(d.input), 33);
            })()");
            await Task.Delay(700);
            File.WriteAllText(Path.Combine(directory, "interaction-state.json"), await window.EvaluateForDiagnostics("(() => { const d=window.petDiagnostics, s=d.interactions; return {headX:d.headX,eyes:s.eyes,head:s.head,settings:s.settings,input:s.input,x:s.x,dt:s.dt,paused:s.paused,busy:s.busy,ids:s.core._parameterIds}; })()"));
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.headX > 0") == "true", "Mouse right did not move the head");
            app.Pet.Capture(Path.Combine(directory, "pet-look-right.png"));
            await window.EvaluateForDiagnostics("window.petDiagnostics.input.x = -1");
            await Task.Delay(700);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.headX < 0") == "true", "Mouse left did not move the head");
            app.Pet.Capture(Path.Combine(directory, "pet-look-left.png"));
            await window.EvaluateForDiagnostics("window.petDiagnostics.input.typing = true");
            await Task.Delay(3200);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.typing && window.petDiagnostics.keyboard > .5") == "true", "Typing did not continue beyond one motion cycle");
            app.Pet.Capture(Path.Combine(directory, "pet-typing.png"));
            await window.EvaluateForDiagnostics("window.petDiagnostics.input.typing = false");
            await Task.Delay(600);
            Check(await window.EvaluateForDiagnostics("!window.petDiagnostics.interactions.typing && Math.abs(window.petDiagnostics.keyboard-window.petDiagnostics.baseline)<.001") == "true", "Keyboard prop did not restore after typing");
            app.Pet.Capture(Path.Combine(directory, "pet-typing-stopped.png"));
            await window.EvaluateForDiagnostics("window.petDiagnostics.input.typing = true");
            await Task.Delay(400); app.Pet.Notify(6); await Task.Delay(400);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.paused && !window.petDiagnostics.interactions.typing") == "true", "Pause did not suspend typing");
            app.Pet.Notify(7);
            bool resumed = false;
            for (int i = 0; i < 60; i++) {
                await Task.Delay(200);
                if (await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.typing") == "true") { resumed = true; break; }
            }
            Check(resumed, "Typing did not resume after the celebration finished");
            await window.EvaluateForDiagnostics("clearInterval(window.petDiagnostics.timer); window.petDiagnostics.interactions.receive({x:0,y:0,typing:false})");
            await Task.Delay(500);
            app.Pet.SetInteractions(false, false, false, "editors");
            Check(!window.InputHookInstalled, "Disabled typing retained its hook");
            app.Pet.SetInteractions(true, false, true, "editors");
            Check(window.InputHookInstalled, "Input hook did not reinstall");
        }

        private static void CheckTransparency(Window window)
        {
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            Point? blank = null, body = null;
            for (int y = 75; y < bitmap.PixelHeight - 5; y += 5)
                for (int x = 5; x < bitmap.PixelWidth - 5; x += 5) {
                    byte alpha = pixels[(y * bitmap.PixelWidth + x) * 4 + 3];
                    if (alpha == 0 && !blank.HasValue) blank = new Point(x, y);
                    if (alpha == 255 && !body.HasValue) body = new Point(x, y);
                }
            Check(blank.HasValue && body.HasValue, "Expected fully transparent margins and an opaque character");
            IntPtr own = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            Point blankScreen = window.PointToScreen(blank.Value), bodyScreen = window.PointToScreen(body.Value);
            Check(WindowFromPoint(new NativePoint(blankScreen)) != own, "Transparent margin intercepts desktop hit testing");
            Check(WindowFromPoint(new NativePoint(bodyScreen)) == own, "Character body is not a native mouse target");
        }

        private static async Task Benchmark(App app, MainWindow panel, string directory)
        {
            var report = new StringBuilder("limit,mean_render_fps,samples,seconds,process_tree_cpu_core_percent,summed_working_set_mib\r\n");
            foreach (int limit in new[] { 30, 60, 90, 120 }) {
                panel.SelectFrameLimitForDiagnostics(limit);
                Check(app.Pet.Settings.FrameLimit == limit, "FPS selection did not reach controller");
                Check(!app.Pet.RenderFps.HasValue, "Old FPS was not cleared on selection");
                Check(PetSettings.Load().FrameLimit == limit, "FPS selection was not persisted");
                await Task.Delay(2500);
                var samples = new List<double>();
                EventHandler sample = delegate { if (app.Pet.RenderFps.HasValue) samples.Add(app.Pet.RenderFps.Value); };
                app.Pet.MetricsChanged += sample;
                var start = ProcessTreeSnapshot();
                var clock = Stopwatch.StartNew();
                try { await Task.Delay(5000); }
                finally { app.Pet.MetricsChanged -= sample; }
                var end = ProcessTreeSnapshot();
                clock.Stop();
                Check(samples.Count >= 2, "No live renderer FPS samples for limit " + limit);
                double fps = 0, cpu = 0, memory = 0;
                foreach (double value in samples) fps += value;
                fps /= samples.Count;
                Check(fps > 0 && fps <= limit + 3, "FPS counter is invalid or limiter failed: " + fps);
                foreach (var pair in end) {
                    memory += pair.Value.Item2;
                    Tuple<double, long> before;
                    if (start.TryGetValue(pair.Key, out before)) cpu += Math.Max(0, pair.Value.Item1 - before.Item1);
                }
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:F1},{2},{3:F2},{4:F1},{5:F1}",
                    limit, fps, samples.Count, clock.Elapsed.TotalSeconds, cpu / clock.Elapsed.TotalSeconds * 100, memory / 1048576));
                File.WriteAllText(Path.Combine(directory, "fps-benchmark.csv"), report.ToString());
                Capture(panel, Path.Combine(directory, "console-fps-" + limit + ".png"));
            }
            panel.SelectFrameLimitForDiagnostics(30);
        }

        // Toolhelp snapshots keep measurements scoped to this app and its WebView2
        // descendants; unrelated browser processes are never included.
        private static Dictionary<int, Tuple<double, long>> ProcessTreeSnapshot()
        {
            var parents = new Dictionary<int, int>();
            IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
            if (snapshot == new IntPtr(-1)) throw new InvalidOperationException("Process snapshot failed");
            try {
                var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
                if (Process32First(snapshot, ref entry)) do { parents[(int)entry.Id] = (int)entry.Parent; } while (Process32Next(snapshot, ref entry));
            } finally { CloseHandle(snapshot); }
            var ids = new HashSet<int> { Process.GetCurrentProcess().Id };
            bool changed;
            do { changed = false; foreach (var pair in parents) if (ids.Contains(pair.Value)) changed |= ids.Add(pair.Key); } while (changed);
            var result = new Dictionary<int, Tuple<double, long>>();
            foreach (int id in ids) try {
                using (var process = Process.GetProcessById(id)) result[id] = Tuple.Create(process.TotalProcessorTime.TotalSeconds, process.WorkingSet64);
            } catch (ArgumentException) { } catch (InvalidOperationException) { }
            return result;
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; public NativePoint(Point p) { X = (int)p.X; Y = (int)p.Y; } }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ProcessEntry {
            public uint Size, Usage, Id; public IntPtr Heap; public uint Module, Threads, Parent; public int Priority; public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string File;
        }
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
        [DllImport("kernel32.dll")] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        private static void Capture(Window window, string path)
        {
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
