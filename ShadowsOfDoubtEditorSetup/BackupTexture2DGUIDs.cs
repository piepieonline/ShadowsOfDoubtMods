using Newtonsoft.Json;

class BackupTexture2DGUIDs
    {
    public static void CreateBackup(string textureFolder, string outputPath)
    {
        var map = new Dictionary<string, string>();

        foreach (var file in Directory.GetFiles(textureFolder, "*.meta", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            var guid = content.Split('\n')[1].Substring(6);
            var fileName = file.Split('\\').Last().Split('.')[0];

            map[fileName] = guid;
        }

        File.WriteAllText(outputPath, JsonConvert.SerializeObject(map));
    }
    }
