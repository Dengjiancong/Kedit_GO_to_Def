using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace Kedit.Console
{
    internal sealed class PetModel
    {
        public string PathName { get; private set; }
        public string DirectoryName { get { return Path.GetDirectoryName(PathName); } }
        public string FileName { get { return Path.GetFileName(PathName); } }
        public int ResourceCount { get; private set; }

        public static PetModel Validate(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("请选择 Live2D 的 .model3.json 文件。");
            path = Path.GetFullPath(path);
            if (!File.Exists(path)) throw new FileNotFoundException("找不到模型入口，请重新选择。", path);
            if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("模型入口文件过大。");
            var json = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
            object value;
            if (json == null || !json.TryGetValue("FileReferences", out value)) throw new InvalidDataException("模型缺少 FileReferences。");
            var refs = value as Dictionary<string, object>;
            if (refs == null || !refs.ContainsKey("Moc") || !refs.ContainsKey("Textures"))
                throw new InvalidDataException("模型缺少 Moc 或 Textures。");
            var files = new List<string>();
            files.Add(refs["Moc"] as string);
            var textures = refs["Textures"] as object[];
            if (textures == null || textures.Length == 0) throw new InvalidDataException("模型没有贴图。");
            foreach (object texture in textures) files.Add(texture as string);
            foreach (string key in new[] { "Physics", "Pose", "DisplayInfo", "UserData" })
                if (refs.TryGetValue(key, out value)) files.Add(value as string);
            if (refs.TryGetValue("Expressions", out value)) CollectFiles(value, files);
            if (refs.TryGetValue("Motions", out value)) CollectFiles(value, files);
            string directory = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar;
            foreach (string file in files)
            {
                if (string.IsNullOrWhiteSpace(file) || Path.IsPathRooted(file) || file.Contains(":"))
                    throw new InvalidDataException("模型含无效的资源路径。");
                string resource = Path.GetFullPath(Path.Combine(directory, file));
                if (!resource.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("模型资源必须位于所选模型目录内：" + file);
                if (!File.Exists(resource)) throw new FileNotFoundException("模型缺少资源：" + file, resource);
            }
            return new PetModel { PathName = path, ResourceCount = files.Count };
        }

        private static void CollectFiles(object node, List<string> files)
        {
            var map = node as Dictionary<string, object>;
            if (map != null)
            {
                foreach (var pair in map)
                    if (pair.Key == "File" || pair.Key == "Sound") files.Add(pair.Value as string);
                    else CollectFiles(pair.Value, files);
                return;
            }
            var array = node as object[];
            if (array != null) foreach (object item in array) CollectFiles(item, files);
        }
    }
}
