using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Kedit.Console
{
    public sealed class PetSettings
    {
        public string ModelPath { get; set; }
        public bool Enabled { get; set; }
        public bool Topmost { get; set; }
        public bool ClickThrough { get; set; }
        public double Size { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public bool HasPosition { get; set; }
        public int FrameLimit { get; set; }
        public bool MouseFollow { get; set; }
        public bool HeadFollow { get; set; }
        public bool TypingEnabled { get; set; }
        public string TypingScope { get; set; }
        public double FollowAmount { get; set; }
        public double FollowSensitivity { get; set; }
        public double FollowSpeed { get; set; }

        public PetSettings() { ModelPath = ""; Size = 360; Topmost = true; FrameLimit = 30; MouseFollow = HeadFollow = TypingEnabled = true; TypingScope = "editors"; FollowAmount = 45; FollowSensitivity = 2; FollowSpeed = 1.5; }
        public static double Bound(double value, double min, double max, double fallback) { return IsFinite(value) ? Math.Max(min, Math.Min(max, value)) : fallback; }
        public static int NormalizeFrameLimit(int value) { return value == 60 || value == 90 || value == 120 ? value : 30; }

        public static PetSettings Load()
        {
            try
            {
                string path = Path.Combine(PetRuntime.DataDirectory, "settings.json");
                if (!File.Exists(path)) return new PetSettings();
                var settings = new JavaScriptSerializer().Deserialize<PetSettings>(File.ReadAllText(path, Encoding.UTF8));
                if (settings == null) return new PetSettings();
                settings.Size = IsFinite(settings.Size) ? Math.Max(180, Math.Min(1200, settings.Size)) : 360;
                settings.FrameLimit = NormalizeFrameLimit(settings.FrameLimit);
                settings.TypingScope = settings.TypingScope == "all" ? "all" : "editors";
                settings.FollowAmount = Bound(settings.FollowAmount, 10, 100, 45);
                settings.FollowSensitivity = Bound(settings.FollowSensitivity, .5, 4, 2);
                settings.FollowSpeed = Bound(settings.FollowSpeed, .5, 3, 1.5);
                settings.HasPosition &= IsFinite(settings.Left) && IsFinite(settings.Top);
                return settings;
            }
            catch (Exception ex) { PetRuntime.Log("Settings load: " + ex.Message); return new PetSettings(); }
        }

        public void Save()
        {
            Directory.CreateDirectory(PetRuntime.DataDirectory);
            string path = Path.Combine(PetRuntime.DataDirectory, "settings.json");
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        private static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
