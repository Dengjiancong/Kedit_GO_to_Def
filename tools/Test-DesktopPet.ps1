[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ModelPath)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testDirectory = Join-Path $root '.pet-test\unit'
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
$sources = @('PetRuntime.cs', 'PetSettings.cs', 'PetModel.cs') | ForEach-Object {
    Get-Content -LiteralPath (Join-Path $root ('Kedit.Console\Pet\' + $_)) -Raw -Encoding UTF8
}
$testSource = @'
using System;
using System.IO;
using System.Web.Script.Serialization;
namespace Kedit.Console {
    public static class PetChecks {
        private static void Assert(bool condition, string text) { if (!condition) throw new Exception(text); }
        private static void Reject(string path) {
            bool rejected = false;
            try { PetModel.Validate(path); } catch (Exception) { rejected = true; }
            Assert(rejected, "Expected invalid model to be rejected: " + path);
        }
        public static string Run(string directory, string modelPath) {
            PetRuntime.Configure(directory);
            var model = PetModel.Validate(modelPath);
            Assert(model.ResourceCount >= 2, "User model references were not checked");
            string fixture = Path.Combine(directory, "fixture"); Directory.CreateDirectory(fixture);
            string entry = Path.Combine(fixture, "test.model3.json");
            File.WriteAllText(entry, "{}"); Reject(entry);
            File.WriteAllText(entry, "not json"); Reject(entry);
            File.WriteAllText(entry, "{\"FileReferences\":{\"Moc\":\"missing.moc3\",\"Textures\":[\"missing.png\"]}}"); Reject(entry);
            File.WriteAllText(Path.Combine(directory, "outside.moc3"), "test");
            File.WriteAllText(entry, "{\"FileReferences\":{\"Moc\":\"../outside.moc3\",\"Textures\":[\"../outside.moc3\"]}}"); Reject(entry);
            File.WriteAllText(entry, "{\"FileReferences\":{\"Moc\":\"https://example.com/x\",\"Textures\":[\"x.png\"]}}"); Reject(entry);
            var settings = new PetSettings { ModelPath = model.PathName, Enabled = true, Size = 420, Left = -240, Top = 30, HasPosition = true, ClickThrough = true, FrameLimit = 120 };
            settings.Save(); var loaded = PetSettings.Load();
            Assert(loaded.Enabled && loaded.ClickThrough && loaded.Size == 420 && loaded.Left == -240 && loaded.ModelPath == model.PathName && loaded.FrameLimit == 120, "Settings did not round-trip");
            foreach (int limit in new[] { 30, 60, 90, 120 }) { settings.FrameLimit = limit; settings.Save(); Assert(PetSettings.Load().FrameLimit == limit, "FPS preset did not persist"); }
            settings.MouseFollow = false; settings.HeadFollow = true; settings.TypingEnabled = false; settings.TypingScope = "all"; settings.Save();
            loaded = PetSettings.Load(); Assert(!loaded.MouseFollow && loaded.HeadFollow && !loaded.TypingEnabled && loaded.TypingScope == "all", "Interaction settings did not persist");
            settings.TypingScope = "invalid"; settings.Save(); Assert(PetSettings.Load().TypingScope == "editors", "Invalid input scope was not normalized");
            settings.FrameLimit = 999; settings.Save(); Assert(PetSettings.Load().FrameLimit == 30, "Invalid frame limit not normalized");
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"Size\":360}"); Assert(PetSettings.Load().FrameLimit == 30, "Old settings must default to 30 FPS");
            loaded = PetSettings.Load(); Assert(loaded.MouseFollow && loaded.TypingEnabled && loaded.HeadFollow && loaded.TypingScope == "editors", "Missing interaction settings must default to all enabled");
            Assert(loaded.FollowAmount == 45 && loaded.FollowSensitivity == 2 && loaded.FollowSpeed == 1.5, "Follow defaults incorrect");
            Assert(loaded.ScrollRate == 600 && loaded.ScrollSeconds == 5, "Old config must gain scroll defaults");
            Assert(loaded.BubbleOffsetPercent == 30 && loaded.MouthAmount == 75, "Existing settings did not gain presentation defaults");
            Assert(loaded.SwordSensitivity == 1.5 && loaded.SwordAmount == 65 && loaded.SwordHeadAmount == 25, "Sword defaults incorrect");
            Assert(loaded.SwordHeadSensitivity==1,"Head sensitivity default incorrect");
            Assert(loaded.SwordHeadSpeed==1,"Old settings must gain 1x head speed");
            settings.SwordHeadSpeed=2;settings.Save();Assert(PetSettings.Load().SwordHeadSpeed==2,"Head speed did not persist");
            settings.SwordHeadSpeed=99;settings.Save();Assert(PetSettings.Load().SwordHeadSpeed==3,"Head speed not bounded");
            settings.SwordHeadSensitivity=3;settings.Save();Assert(PetSettings.Load().SwordHeadSensitivity==3,"Head sensitivity did not persist");
            settings.SwordHeadSensitivity=99;settings.Save();Assert(PetSettings.Load().SwordHeadSensitivity==4,"Head sensitivity not bounded");
            settings.SwordSensitivity=2.5; settings.SwordAmount=90; settings.SwordHeadAmount=10; settings.Save(); loaded=PetSettings.Load();
            Assert(loaded.SwordSensitivity==2.5 && loaded.SwordAmount==90 && loaded.SwordHeadAmount==10, "Sword settings did not persist");
            settings.SwordHeadAmount=100;settings.Save();Assert(PetSettings.Load().SwordHeadAmount==100,"100 percent head setting was clipped");
            settings.BubbleOffsetPercent = 45; settings.MouthAmount = 90; settings.Save(); loaded = PetSettings.Load();
            Assert(loaded.BubbleOffsetPercent == 45 && loaded.MouthAmount == 90, "Presentation settings did not persist");
            settings.BubbleOffsetPercent = 999; settings.MouthAmount = -1; settings.Save(); loaded = PetSettings.Load();
            Assert(loaded.BubbleOffsetPercent == 70 && loaded.MouthAmount == 10, "Presentation settings not bounded");
            Assert(loaded.CareEnabled && loaded.CareMinutes == 20 && !loaded.RestEnabled && loaded.FavoriteCombination.Length == 0, "Companion defaults incorrect");
            settings.CareEnabled = false; settings.CareMinutes = 30; settings.RestEnabled = true; settings.QuietUntil = 1800000000000;
            settings.FavoriteModel = model.PathName; settings.FavoriteCombination = new[] { "hand-pot", "emote-shy" }; settings.Save(); loaded = PetSettings.Load();
            Assert(!loaded.CareEnabled && loaded.CareMinutes == 30 && loaded.RestEnabled && loaded.QuietUntil == 1800000000000 && loaded.FavoriteCombination.Length == 2 && loaded.FavoriteModel == model.PathName, "Companion settings did not persist");
            settings.ScrollRate = 900; settings.ScrollSeconds = 6; settings.Save(); loaded = PetSettings.Load();
            Assert(loaded.ScrollRate == 900 && loaded.ScrollSeconds == 6, "Scroll settings did not persist");
            settings.ScrollRate = -1; settings.ScrollSeconds = 999; settings.Save(); loaded = PetSettings.Load();
            Assert(loaded.ScrollRate == 60 && loaded.ScrollSeconds == 30, "Scroll settings not bounded");
            settings.FollowAmount = 80; settings.FollowSensitivity = 3; settings.FollowSpeed = 2; settings.Save();
            loaded = PetSettings.Load(); Assert(loaded.FollowAmount == 80 && loaded.FollowSensitivity == 3 && loaded.FollowSpeed == 2, "Follow values did not persist");
            settings.FollowAmount = 999; settings.FollowSensitivity = -1; settings.FollowSpeed = 999; settings.Save();
            loaded = PetSettings.Load(); Assert(loaded.FollowAmount == 100 && loaded.FollowSensitivity == .5 && loaded.FollowSpeed == 3, "Follow values not bounded");
            Assert(PetSettings.Bound(double.NaN, 10, 100, 45) == 45, "NaN follow value did not recover");
            settings.Size = 99999; settings.Save(); Assert(PetSettings.Load().Size == 1200, "Invalid size not bounded");
            File.WriteAllText(Path.Combine(directory, "settings.json"), "broken");
            Assert(!PetSettings.Load().Enabled && PetSettings.Load().Size == 360, "Corrupt settings did not recover");
            return "PASS: real model (" + model.ResourceCount + " resources), malformed JSON, missing resources, path traversal, remote paths, settings persistence, bounds and recovery.";
        }
    }
}
'@
# Namespace using directives must remain at the start of each separate compilation unit.
$sourceFiles = @()
for ($i=0; $i -lt $sources.Count; $i++) {
    $file = Join-Path $testDirectory ("source-$i.cs")
    Set-Content -LiteralPath $file -Value $sources[$i] -Encoding UTF8
    $sourceFiles += $file
}
$harness = Join-Path $testDirectory 'checks.cs'
Set-Content -LiteralPath $harness -Value $testSource -Encoding UTF8
$sourceFiles += $harness
Add-Type -Path $sourceFiles -ReferencedAssemblies System.Web.Extensions.dll
[Kedit.Console.PetChecks]::Run($testDirectory, (Resolve-Path -LiteralPath $ModelPath).Path)
