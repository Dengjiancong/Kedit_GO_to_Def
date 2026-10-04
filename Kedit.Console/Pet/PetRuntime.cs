using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Kedit.Console
{
    internal static class PetRuntime
    {
        public static string DataDirectory { get; private set; }
        public static string AssetDirectory { get; private set; }
        private static bool initialized;

        public static void Configure(string directory)
        {
            DataDirectory = directory;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        public static void Prepare()
        {
            if (initialized) return;
            var assembly = Assembly.GetExecutingAssembly();
            string hash;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(assembly.Location))
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").Substring(0, 16);
            AssetDirectory = Path.Combine(DataDirectory, "Runtime", hash);
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith("PetWeb/") && !name.StartsWith("PetRuntime/")) continue;
                string path = Path.Combine(AssetDirectory, name.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (!File.Exists(path))
                    using (var input = assembly.GetManifestResourceStream(name))
                    using (var output = File.Create(path)) input.CopyTo(output);
            }
            string loader = Path.Combine(AssetDirectory, "PetRuntime", IntPtr.Size == 8 ? "x64" : "x86", "WebView2Loader.dll");
            if (LoadLibrary(loader) == IntPtr.Zero)
                throw new InvalidOperationException("无法加载 WebView2Loader，错误码 " + Marshal.GetLastWin32Error());
            initialized = true;
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            if (name != "Microsoft.Web.WebView2.Core" && name != "Microsoft.Web.WebView2.Wpf") return null;
            Prepare();
            return Assembly.LoadFrom(Path.Combine(AssetDirectory, "PetRuntime", name + ".dll"));
        }

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                string path = Path.Combine(DataDirectory, "pet.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.WriteAllText(path, "");
                File.AppendAllText(path, DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);
    }
}
