using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DDSLoader
{
    public class DDSContentSource
    {
        public const string ManifestFileName = "ddsmanifest.json";

        public DirectoryInfo directory;

        readonly Dictionary<string, List<string>> filesByFolder = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public string Name => directory.Parent != null ? directory.Parent.Name : directory.Name;

        public static DDSContentSource FromFolderScan(DirectoryInfo directory)
        {
            var source = new DDSContentSource() { directory = directory };

            foreach (var filePath in Directory.GetFiles(directory.FullName, "*", SearchOption.AllDirectories))
            {
                source.Add(NormalizeFolder(Path.GetDirectoryName(filePath).Substring(directory.FullName.Length)), filePath);
            }

            return source;
        }

        public static DDSContentSource FromManifest(DirectoryInfo directory)
        {
            var manifestPath = Path.Combine(directory.FullName, ManifestFileName);
            dynamic manifest = AssetBundleLoader.JsonLoader.NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(File.ReadAllText(manifestPath));

            if (!manifest.Value<bool>("enabled"))
            {
                DDSLoaderPlugin.PluginLogger.LogInfo($"Not loading DDS manifest: {directory.Parent?.Name} (Disabled)");
                return null;
            }

            var source = new DDSContentSource() { directory = directory };

            foreach (var entry in manifest["files"])
            {
                string relativeFilePath = entry.Name;
                string folder = entry.Value.ToString();

                var filePath = Path.Combine(directory.FullName, relativeFilePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(filePath))
                {
                    DDSLoaderPlugin.PluginLogger.LogError($"Failed to load: {filePath} (Listed in {ManifestFileName}, but not found)");
                    continue;
                }

                source.Add(NormalizeFolder(folder), filePath);
            }

            return source;
        }

        public List<string> GetFiles(string folder, string extension)
        {
            if (!filesByFolder.TryGetValue(folder, out var files))
            {
                return new List<string>();
            }

            return files.Where(filePath => Path.GetExtension(filePath).Equals(extension, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public bool HasStringsForLanguage(string language) => StringFolders(language).Any();

        public List<string> GetStringFiles(string language)
        {
            return StringFolders(language)
                .SelectMany(folder => filesByFolder[folder])
                .Where(filePath => Path.GetExtension(filePath).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        IEnumerable<string> StringFolders(string language)
        {
            var languageFolder = NormalizeFolder($"Strings/{language}");
            return filesByFolder.Keys.Where(folder => folder.Equals(languageFolder, StringComparison.OrdinalIgnoreCase) || folder.StartsWith(languageFolder + "/", StringComparison.OrdinalIgnoreCase));
        }

        void Add(string folder, string filePath)
        {
            if (!filesByFolder.TryGetValue(folder, out var files))
            {
                files = new List<string>();
                filesByFolder[folder] = files;
            }

            files.Add(filePath);
        }

        static string NormalizeFolder(string folder) => folder.Replace('\\', '/').Trim('/');
    }
}
