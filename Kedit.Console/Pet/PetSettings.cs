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
        public int ScrollRate { get; set; }
        public int ScrollSeconds { get; set; }
        public bool CareEnabled { get; set; }
        public double BubbleOffsetPercent { get; set; }
        public double MouthAmount { get; set; }
        public double SwordSensitivity { get; set; }
        public double SwordAmount { get; set; }
        public double SwordHeadAmount { get; set; }
        public double SwordHeadSensitivity { get; set; }
        public double SwordHeadSpeed { get; set; }
        public int CareMinutes { get; set; }
        public bool RestEnabled { get; set; }
        public long QuietUntil { get; set; }
        public string[] FavoriteCombination { get; set; }
        public string FavoriteModel { get; set; }

        public PetSettings() { ModelPath = ""; Size = 360; Topmost = true; FrameLimit = 30; MouseFollow = HeadFollow = TypingEnabled = true; TypingScope = "editors"; FollowAmount = 45; FollowSensitivity = 2; FollowSpeed = 1.5; ScrollRate = 600; ScrollSeconds = 5; CareEnabled = true; CareMinutes = 20; BubbleOffsetPercent = 30; MouthAmount = 75; SwordSensitivity = 1.5; SwordAmount = 65; SwordHeadAmount = 25; SwordHeadSensitivity = 1; SwordHeadSpeed = 1; FavoriteCombination = new string[0]; FavoriteModel = ""; }
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
                settings.ScrollRate = Math.Max(60, Math.Min(3000, settings.ScrollRate));
                settings.ScrollSeconds = Math.Max(1, Math.Min(30, settings.ScrollSeconds));
                settings.BubbleOffsetPercent = Bound(settings.BubbleOffsetPercent, 0, 70, 30);
                settings.SwordSensitivity = Bound(settings.SwordSensitivity, .5, 3, 1.5);
                settings.SwordAmount = Bound(settings.SwordAmount, 0, 100, 65);
                settings.SwordHeadAmount = Bound(settings.SwordHeadAmount, 0, 100, 25);
                settings.SwordHeadSensitivity = Bound(settings.SwordHeadSensitivity, .5, 4, 1);
                settings.SwordHeadSpeed = Bound(settings.SwordHeadSpeed, .5, 3, 1);
                settings.MouthAmount = Bound(settings.MouthAmount, 10, 100, 75);
                settings.CareMinutes = settings.CareMinutes == 10 || settings.CareMinutes == 30 ? settings.CareMinutes : 20;
                settings.QuietUntil = Math.Max(0, Math.Min(253402300799999L, settings.QuietUntil));
                settings.FavoriteCombination = settings.FavoriteCombination ?? new string[0];
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
