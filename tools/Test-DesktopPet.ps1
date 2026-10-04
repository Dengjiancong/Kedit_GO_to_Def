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
            var settings = new PetSettings { ModelPath = model.PathName, Enabled = true, Size = 420, Left = -240, Top = 30, HasPosition = true, ClickThrough = true };
            settings.Save(); var loaded = PetSettings.Load();
            Assert(loaded.Enabled && loaded.ClickThrough && loaded.Size == 420 && loaded.Left == -240 && loaded.ModelPath == model.PathName, "Settings did not round-trip");
            settings.Size = 99999; settings.Save(); Assert(PetSettings.Load().Size == 720, "Invalid size not bounded");
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
