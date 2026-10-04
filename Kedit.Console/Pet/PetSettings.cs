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

        public PetSettings() { ModelPath = ""; Size = 360; Topmost = true; }

        public static PetSettings Load()
        {
            try
            {
                string path = Path.Combine(PetRuntime.DataDirectory, "settings.json");
                if (!File.Exists(path)) return new PetSettings();
                var settings = new JavaScriptSerializer().Deserialize<PetSettings>(File.ReadAllText(path, Encoding.UTF8));
                if (settings == null) return new PetSettings();
                settings.Size = IsFinite(settings.Size) ? Math.Max(180, Math.Min(720, settings.Size)) : 360;
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
