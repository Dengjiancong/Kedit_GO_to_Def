using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Kedit.Console
{
    // The hook retains only a bounded activity count. No key values or text leave it.
    internal sealed class PetInput : IDisposable
    {
        private readonly PetWindow window;
        private readonly PetSettings settings;
        private readonly Action<double, double, int> publish;
        private readonly DispatcherTimer timer;
        private readonly HookProc callback;
        private readonly int ownProcessId = Process.GetCurrentProcess().Id;
        private IntPtr hook, allowedWindow, foreground;
        private int pendingPresses;
        private long lastPress;
        private bool allowed, typingEnabled;
        private NativePoint previousCursor;
        private long cursorTime;
        internal double MouseVX { get; private set; }
        internal double MouseVY { get; private set; }
        internal bool HookInstalled { get { return hook != IntPtr.Zero; } }

        public PetInput(PetWindow window, PetSettings settings, Action<double, double, int> publish)
        {
            this.window = window; this.settings = settings; this.publish = publish;
            callback = OnKeyboard;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            timer.Tick += Tick;
        }

        public void Configure(bool typingAvailable)
        {
            typingEnabled = settings.TypingEnabled && typingAvailable;
            cursorTime=0; MouseVX=MouseVY=0;
            pendingPresses = 0; foreground = allowedWindow = IntPtr.Zero;
            if (typingEnabled && hook == IntPtr.Zero) {
                hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
                if (hook == IntPtr.Zero) PetRuntime.Log("Keyboard activity hook unavailable: " + Marshal.GetLastWin32Error());
            }
            if (!typingEnabled && hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            if (settings.MouseFollow || typingEnabled) timer.Start();
            else { timer.Stop(); publish(0, 0, 0); }
        }

        private void Tick(object sender, EventArgs e)
        {
            IntPtr current = typingEnabled ? GetForegroundWindow() : IntPtr.Zero;
            if (current != foreground) {
                foreground = current; pendingPresses = 0; allowed = false;
                uint pid; GetWindowThreadProcessId(current, out pid);
                try {
                    using (var process = Process.GetProcessById((int)pid)) {
                        string name = process.ProcessName;
                        allowed = pid != (uint)ownProcessId &&
                            (settings.TypingScope == "all" || name.Equals("kedit", StringComparison.OrdinalIgnoreCase) || name.Equals("devenv", StringComparison.OrdinalIgnoreCase));
                    }
                } catch (ArgumentException) { } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                allowedWindow = allowed ? current : IntPtr.Zero;
            }
            double x = 0, y = 0;
            NativePoint point;
            MouseVX=MouseVY=0;
            if (settings.MouseFollow && GetCursorPos(out point)) {
                long timestamp=Stopwatch.GetTimestamp();
                double elapsed=(timestamp-cursorTime)/(double)Stopwatch.Frequency;
                if(cursorTime!=0 && elapsed>.005 && elapsed<.25 && !window.IsDraggingForInput) {
                    var source=PresentationSource.FromVisual(window);
                    Vector delta=new Vector(point.X-previousCursor.X,point.Y-previousCursor.Y);
                    if(source!=null) delta=source.CompositionTarget.TransformFromDevice.Transform(delta);
                    if(delta.Length<2000) { MouseVX=delta.X/elapsed/1000; MouseVY=-delta.Y/elapsed/1000; }
                }
                previousCursor=point; cursorTime=timestamp;
                Point local = window.PointFromScreen(new Point(point.X, point.Y));
                x = Clamp((local.X - window.ActualWidth * .5) / Math.Max(100, window.ActualWidth));
                y = Clamp((65 + (window.ActualHeight - 65) * .5 - local.Y) / Math.Max(100, window.ActualHeight - 65));
            }
            else cursorTime=0;
            bool fresh = (Stopwatch.GetTimestamp() - lastPress) * 1000.0 / Stopwatch.Frequency < 250;
            int presses = typingEnabled && allowed && fresh ? pendingPresses : 0;
            pendingPresses = 0;
            publish(x, y, presses);
        }

        internal static bool IsTypingKey(int key)
        {
            return key >= 0x30 && key <= 0x5A || key >= 0x60 && key <= 0x6F ||
                key >= 0xBA && key <= 0xC0 || key >= 0xDB && key <= 0xDF ||
                key == 0xE2 || key == 0x20 || key == 0x08 || key == 0x0D || key == 0xE5;
        }

        private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && (message.ToInt32() == 0x100 || message.ToInt32() == 0x104) &&
                allowedWindow != IntPtr.Zero && GetForegroundWindow() == allowedWindow) {
                var key = (KeyboardData)Marshal.PtrToStructure(data, typeof(KeyboardData));
                if (AcceptKey((int)key.Key, key.Flags, Down(0x11) || Down(0x12) || Down(0x5B) || Down(0x5C))) {
                    pendingPresses = Math.Min(32, pendingPresses + 1); lastPress = Stopwatch.GetTimestamp();
                }
            }
            return CallNextHookEx(hook, code, message, data);
        }
        private static bool Down(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
        internal static bool AcceptKey(int key, uint flags, bool modifier) { return !modifier && (flags & 0x32) == 0 && IsTypingKey(key); }
        private static double Clamp(double value) { return Math.Max(-1, Math.Min(1, value)); }
        public void Dispose()
        {
            timer.Stop(); allowedWindow = IntPtr.Zero; pendingPresses = 0;
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        }
        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct KeyboardData { public uint Key, ScanCode, Flags, Time; public UIntPtr ExtraInfo; }
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
    }
}
