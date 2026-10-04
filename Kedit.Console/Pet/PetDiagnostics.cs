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
                if (selfTest && Array.IndexOf(Environment.GetCommandLineArgs(), "--probe-typing") >= 0) {
                    await ProbeTyping(app, directory); app.ExitConsole(); return;
                }
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
                File.WriteAllText(Path.Combine(directory, "result.txt"), "PASS: adjustable gaze, single-key strokes, sustained speed/duration settings, authored scrolling with complete final cycle, real 60-second keyboard retention, pause/resume priority, hook installation/removal, original-canvas rendering, alpha-zero blank hit-through, body hit target, NOACTIVATE, click-through on/off, resize, event cues, 30/60/90/120 UI settings and real FPS reports, close/reopen console, dispose/re-enable pet. Typing activity was supplied by the diagnostic driver, not physical keyboard input. See fps-benchmark.csv for measured results.");
                app.ExitConsole();
            }
            catch (Exception ex) {
                PetRuntime.Log("Diagnostics failed: " + ex);
                File.WriteAllText(Path.Combine(directory, "result.txt"), "FAIL: " + ex);
                if (selfTest) app.ExitConsole();
            }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        private static async Task ProbeTyping(App app, string directory)
        {
            app.Pet.SetInteractions(false, false, false, "editors");
            var window = app.Pet.DiagnosticWindow;
            await window.EvaluateForDiagnostics(@"(() => {
                const d=window.petDiagnostics; d.probe={ParamExpression7:1,ParamExpression12:.2,ParamExpression13:0,ParamExpression14:0,ParamExpression15:0};
                d.model.internalModel.on('beforeModelUpdate',()=>{for(const [id,value] of Object.entries(d.probe)) d.model.internalModel.coreModel.setParameterValueById(id,value);});
            })()");
            foreach (var item in new[] { "rest", "knock", "left", "right", "text", "text10", "text20", "text30", "text40", "text50" }) {
                string script = item == "knock" ? "d.probe.ParamExpression12=1" : item == "left" ? "d.probe.ParamExpression13=1" : item == "right" ? "d.probe.ParamExpression13=0;d.probe.ParamExpression14=1" : item == "text" ? "d.probe.ParamExpression15=60" : "";
                if (item.StartsWith("text") && item.Length > 4) script = "d.probe.ParamExpression15=" + item.Substring(4);
                await window.EvaluateForDiagnostics("(()=>{const d=window.petDiagnostics;" + script + "})()");
                await Task.Delay(600); app.Pet.Capture(Path.Combine(directory, "probe-" + item + ".png"));
            }
            File.WriteAllText(Path.Combine(directory,"parameters.json"), await window.EvaluateForDiagnostics("(()=>{const c=window.petDiagnostics.model.internalModel.coreModel; return c._parameterIds.map((id,i)=>({id,min:c.getParameterMinimumValue(i),max:c.getParameterMaximumValue(i),value:c.getParameterDefaultValue(i)}));})()"));
        }

        private static async Task CheckInteractions(App app, string directory)
        {
            var window = app.Pet.DiagnosticWindow;
            app.Pet.ResetInteractions();
            Check(app.Pet.Settings.MouseFollow && app.Pet.Settings.HeadFollow && app.Pet.Settings.TypingEnabled, "All interaction defaults must be enabled");
            Check(window.InputHookInstalled, "Keyboard hook was not installed");
            window.StopInputForDiagnostics();
            Check(!window.InputHookInstalled, "Keyboard hook was not released");
            Check(PetInput.IsTypingKey(0x41) && PetInput.IsTypingKey(0xE5) && !PetInput.IsTypingKey(0x70), "Key classification failed");
            Check(PetInput.AcceptKey(0x41,0,false) && !PetInput.AcceptKey(0x41,0,true) && !PetInput.AcceptKey(0x41,0x10,false) && !PetInput.AcceptKey(0x41,0x20,false), "Modifier/injected-key filter failed");
            await window.EvaluateForDiagnostics(@"(() => {
                const d=window.petDiagnostics; d.input={x:1,y:.5,presses:0};
                d.press=()=>d.interactions.receive({...d.input,presses:1});
                d.timer=setInterval(()=>d.interactions.receive(d.input),33);
                d.model.internalModel.on('beforeModelUpdate',()=>{
                    d.headX=d.model.internalModel.coreModel.getParameterValueById('ParamAngleX');
                    d.hand=d.model.internalModel.coreModel.getParameterValueById('ParamExpression12');
                    d.keyboard=d.model.internalModel.coreModel.getParameterValueById('ParamExpression7');
                });
            })()");
            await Task.Delay(700);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.headX>0") == "true", "Right gaze failed");
            app.Pet.Capture(Path.Combine(directory,"pet-look-right.png"));
            await window.EvaluateForDiagnostics("window.petDiagnostics.input.x=-1");
            await Task.Delay(700);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.headX<0") == "true", "Left gaze failed");
            app.Pet.Capture(Path.Combine(directory,"pet-look-left.png"));
            var panel=(MainWindow)app.MainWindow;
            panel.SetFollowForDiagnostics(80,3,2);
            await Task.Delay(300);
            var saved=PetSettings.Load();
            Check(saved.FollowAmount==80 && saved.FollowSensitivity==3 && saved.FollowSpeed==2, "Follow UI did not persist");
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.settings.followAmount===80") == "true", "Follow UI did not reach renderer");
            panel.SetFollowForDiagnostics(45,2,1.5);
            panel.SetScrollForDiagnostics(900,6); await Task.Delay(150);
            saved=PetSettings.Load();
            Check(saved.ScrollRate==900 && saved.ScrollSeconds==6,"Scroll settings did not persist through UI");
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.settings.scrollSeconds===6 && window.petDiagnostics.interactions.settings.scrollRate===900") == "true","Scroll settings did not reach renderer");
            panel.SetScrollForDiagnostics(600,5); await Task.Delay(100);
            await window.EvaluateForDiagnostics("window.petDiagnostics.press()");
            await Task.Delay(90); app.Pet.Capture(Path.Combine(directory,"pet-single-down.png"));
            await Task.Delay(250);
            Check(await window.EvaluateForDiagnostics("!window.petDiagnostics.interactions.typing && window.petDiagnostics.interactions.strokeCount===1 && window.petDiagnostics.interactions.keyboardWeight===1 && window.petDiagnostics.interactions.textWeight===0") == "true", "Single stroke did not settle with keyboard retained");
            app.Pet.Capture(Path.Combine(directory,"pet-single-up.png"));
            for(int i=0;i<4;i++) { await window.EvaluateForDiagnostics("window.petDiagnostics.press()"); await Task.Delay(500); }
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.textWeight===0") == "true", "Slow typing revealed text");
            app.Pet.Capture(Path.Combine(directory,"pet-slow-no-text.png"));
            await window.EvaluateForDiagnostics("window.petDiagnostics.fastTimer=setInterval(window.petDiagnostics.press,80)");
            await Task.Delay(4000);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.cycleStart===null") == "true","Brief high speed triggered scrolling before sustained duration");
            panel.ShowPetInteractionsForDiagnostics(); await Task.Delay(100);
            Capture(panel,Path.Combine(directory,"console-scroll-progress.png"));
            bool rolling=false;
            for(int i=0;i<100;i++) { await Task.Delay(50); if(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.cycleStart!==null") == "true") {rolling=true;break;} }
            Check(rolling,"Sustained typing failed to trigger scrolling");
            await window.EvaluateForDiagnostics("clearInterval(window.petDiagnostics.fastTimer)");
            Check(await window.EvaluateForDiagnostics("Math.abs(window.petDiagnostics.interactions.textCurve(1.3)-32.167)<.0001 && window.petDiagnostics.interactions.textDuration===2.4") == "true","Authored text curve or duration not preserved");
            await Task.Delay(650);
            double previous=double.Parse(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.textValue"),CultureInfo.InvariantCulture);
            app.Pet.Capture(Path.Combine(directory,"pet-scroll-entry.png"));
            await Task.Delay(650);
            double middle=double.Parse(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.textValue"),CultureInfo.InvariantCulture);
            Check(middle>previous && previous>0,"Text did not keep scrolling after typing stopped");
            app.Pet.Capture(Path.Combine(directory,"pet-scroll-middle.png"));
            await Task.Delay(650);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.textWeight===1") == "true","Final cycle was cut short");
            app.Pet.Capture(Path.Combine(directory,"pet-scroll-exit.png"));
            await Task.Delay(650);
            Check(await window.EvaluateForDiagnostics("!window.petDiagnostics.interactions.typing && window.petDiagnostics.interactions.textWeight===0 && window.petDiagnostics.keyboard>.9 && window.petDiagnostics.headX<0") == "true", "Stopped typing did not retain keyboard and restore gaze");
            app.Pet.Capture(Path.Combine(directory,"pet-stopped-retained.png"));
            // Verify a real minute of wall-clock retention, without shortening the product timeout.
            double age=double.Parse(await window.EvaluateForDiagnostics("performance.now()-window.petDiagnostics.interactions.lastKey"),CultureInfo.InvariantCulture);
            await Task.Delay(Math.Max(1,(int)(59800-age)));
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.keyboardWeight===1") == "true", "Keyboard disappeared before 60 seconds");
            await Task.Delay(700);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.keyboardWeight===0 && window.petDiagnostics.keyboard===0") == "true", "Keyboard did not disappear after 60 seconds");
            app.Pet.Capture(Path.Combine(directory,"pet-after-minute.png"));
            await window.EvaluateForDiagnostics("window.petDiagnostics.press()");
            await Task.Delay(60); app.Pet.Notify(6); await Task.Delay(400);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.paused && window.petDiagnostics.interactions.keyboardWeight===0") == "true", "Pause failed to clear typing state");
            await window.EvaluateForDiagnostics("window.petDiagnostics.press()"); app.Pet.Notify(7);
            for(int i=0;i<60;i++) { await Task.Delay(200); if(await window.EvaluateForDiagnostics("!window.petDiagnostics.interactions.busy") == "true") break; }
            Check(await window.EvaluateForDiagnostics("!window.petDiagnostics.interactions.busy && !window.petDiagnostics.interactions.typing && window.petDiagnostics.interactions.keyboardWeight===0") == "true", "Resume replayed stale input or celebration never ended");
            await window.EvaluateForDiagnostics("window.petDiagnostics.press()"); await Task.Delay(300);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.keyboardWeight===1") == "true", "New input failed after resume");
            await window.EvaluateForDiagnostics("clearInterval(window.petDiagnostics.timer)");
            app.Pet.SetInteractions(true,true,true,"all"); await Task.Delay(350);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.keyboardWeight===0") == "true", "Scope change retained old input");
            app.Pet.SetInteractions(false,false,false,"editors"); Check(!window.InputHookInstalled,"Disabled hook retained");
            app.Pet.ResetInteractions(); Check(window.InputHookInstalled,"Hook did not reinstall");
            File.WriteAllText(Path.Combine(directory,"interaction-state.json"),await window.EvaluateForDiagnostics("(()=>{const d=window.petDiagnostics,s=d.interactions;return {eyes:s.eyes,head:s.head,textDrawables:[...s.textDrawables],settings:s.settings,strokes:s.strokeCount};})()"));
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
