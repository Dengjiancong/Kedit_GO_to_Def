using System;
using System.Runtime.CompilerServices;

namespace Kedit.Console
{
    internal sealed class PetController : IDisposable
    {
        private PetWindow window;
        private DateTime lastCue = DateTime.MinValue;
        public PetSettings Settings { get; private set; }
        public string Status { get; private set; }
        public string InteractionStatus { get; private set; }
        public void SetInteractionStatus(string text) { InteractionStatus = text; RaiseChanged(); }
        public void SetInteractions(bool mouse, bool head, bool typing, string scope)
        {
            Settings.MouseFollow = mouse; Settings.HeadFollow = head; Settings.TypingEnabled = typing;
            Settings.TypingScope = scope == "all" ? "all" : "editors";
            if (window != null) window.ApplyInteractions();
            Save(); RaiseChanged();
        }
        public event EventHandler Changed;
        public event EventHandler MetricsChanged;
        public double? RenderFps { get; private set; }
        internal int MetricsSamples { get; private set; }
        public string FrameRateText { get { return "上限 " + Settings.FrameLimit + "／当前渲染" +
            (RenderFps.HasValue ? "约 " + RenderFps.Value.ToString("0") + " FPS" : IsReady ? "测量中…" : "— FPS"); } }
        internal bool IsReady { get { return window != null && window.IsReady; } }
        internal bool HasWindow { get { return window != null; } }
        internal long WindowStyle { get { return window == null ? 0 : window.ExtendedStyle; } }

        public PetController() { Settings = PetSettings.Load(); Status = "选择模型后开启桌宠。"; }

        public void SelectModel(string path)
        {
            try
            {
                var model = PetModel.Validate(path);
                Settings.ModelPath = model.PathName;
                if (Settings.Enabled) { CloseWindow(); OpenWindow(model); }
                else SetStatus("模型校验通过 · " + model.ResourceCount + " 个资源");
                Save(); RaiseChanged();
            }
            catch (Exception ex) { SetStatus("模型未更换：" + ex.Message); }
        }

        public void SetEnabled(bool enabled)
        {
            if (!enabled) { CloseWindow(); Settings.Enabled = false; SetStatus("桌宠已关闭。"); Save(); return; }
            try
            {
                var model = PetModel.Validate(Settings.ModelPath);
                PetRuntime.Prepare();
                CloseWindow();
                Settings.Enabled = true;
                OpenWindow(model);
                Save(); RaiseChanged();
            }
            catch (Exception ex) { Settings.Enabled = false; CloseWindow(); SetStatus("无法开启桌宠：" + ex.Message); Save(); }
        }

        // Keep WebView2 types out of the startup JIT until the embedded assemblies are ready.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OpenWindow(PetModel model)
        {
            InteractionStatus = "正在检测模型互动能力…";
            SetStatus("正在加载模型…");
            window = new PetWindow(this, model);
            window.Show();
        }

        public void SetOptions(bool topmost, bool clickThrough)
        {
            Settings.Topmost = topmost; Settings.ClickThrough = clickThrough;
            if (window != null) window.ApplyOptions();
            Save(); RaiseChanged();
        }
        public void SetSize(double size)
        {
            if (double.IsNaN(size) || double.IsInfinity(size)) return;
            Settings.Size = Math.Max(180, Math.Min(1200, size));
            if (window != null) window.ApplySize();
            Save(); RaiseChanged();
        }
        public void ResetPosition() { if (window != null) window.ResetPosition(); else Settings.HasPosition = false; Save(); }
        public void Preview() { if (window != null) window.Cue("快捷键联动预览", "celebrate"); else SetStatus("请先开启桌宠。"); }
        internal void Capture(string path) { if (window != null) window.Capture(path); }
        internal PetWindow DiagnosticWindow { get { return window; } }

        public void SetFrameLimit(int limit)
        {
            Settings.FrameLimit = PetSettings.NormalizeFrameLimit(limit);
            SetRenderFps(null);
            if (window != null) window.ApplyFrameLimit();
            Save(); RaiseChanged();
        }
        internal void SetRenderFps(double? fps)
        {
            RenderFps = fps;
            if (fps.HasValue) MetricsSamples++;
            var changed = MetricsChanged; if (changed != null) changed(this, EventArgs.Empty);
        }

        public void Notify(int id)
        {
            if (window == null || id < 1 || id > 8) return;
            var now = DateTime.UtcNow;
            if (id != 6 && id != 7 && (now - lastCue).TotalMilliseconds < 400) return;
            lastCue = now;
            string[] text = { "", "定义跳转", "书签操作", "重做", "正在查找", "快捷键操作提示", "快捷键已屏蔽", "快捷键已恢复", "操作完成" };
            string kind = id == 6 ? "paused" : id == 7 ? "resumed" : id == 8 ? "celebrate" : "keyboard";
            window.Cue(text[id], kind);
        }
        public void SetStatus(string status) { Status = status; PetRuntime.Log(status); RaiseChanged(); }
        public void Save() { try { Settings.Save(); } catch (Exception ex) { SetStatus("设置保存失败：" + ex.Message); } }
        private void RaiseChanged() { var changed = Changed; if (changed != null) changed(this, EventArgs.Empty); }
        private void CloseWindow() { if (window != null) { var old = window; window = null; old.Close(); } SetRenderFps(null); }
        public void Dispose() { CloseWindow(); }
    }
}
