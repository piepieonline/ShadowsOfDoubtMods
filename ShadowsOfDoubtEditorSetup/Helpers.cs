using Newtonsoft.Json;
using System;
using System.IO;
using System.Text.RegularExpressions;

public class Helpers
{
    public static void CopyFolder(DirectoryInfo source, DirectoryInfo target, bool overwrite, bool copy)
    {
        CopyFolder_Internal(source, target.CreateSubdirectory(source.Name), overwrite, copy);

        if (!copy) source.Delete(true);
    }

    private static void CopyFolder_Internal(DirectoryInfo source, DirectoryInfo target, bool overwrite, bool copy)
    {
        foreach (DirectoryInfo dir in source.GetDirectories())
        {
            CopyFolder_Internal(dir, target.CreateSubdirectory(dir.Name), overwrite, copy);
        }

        foreach (FileInfo file in source.GetFiles())
        {
            if(copy)
            {
                file.CopyTo(Path.Join(target.FullName, file.Name), overwrite);
            }
            else
            {
                file.MoveTo(Path.Join(target.FullName, file.Name), overwrite);
            }
        }
    }

    public static void RepointGUIDs(DirectoryInfo directory)
    {
        Remap.types = JsonConvert.DeserializeObject<Dictionary<string, Remap>>(File.ReadAllText("./Tools/TypeRemapping/classMapping.json"))
            .Concat(JsonConvert.DeserializeObject<Dictionary<string, Remap>>(File.ReadAllText("./Tools/TypeRemapping/scriptableMapping.json")))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        RepointGUID_Internal(directory);
    }

    private static void RepointGUID_Internal(DirectoryInfo directory)
    {
        var knownExtensions = new string[] { ".prefab", ".asset", ".mat" };

        foreach (DirectoryInfo dir in directory.GetDirectories())
            RepointGUID_Internal(dir);
        foreach (FileInfo file in directory.GetFiles())
        {
            if(knownExtensions.Contains(file.Extension))
            {
                string content = Regex.Replace(File.ReadAllText(file.FullName), "fileID: (.*), guid: (.{32})", match =>
                {
                    if (Remap.types.ContainsKey(match.Groups[2].Value))
                    {
                        var remapped = Remap.types[match.Groups[2].Value];

                        if (remapped.guid != null)
                        {
                            return $"fileID: {match.Groups[1].Value}, guid: {remapped.guid}";
                        }
                        else
                        {
                            return $"fileID: {remapped.id}, guid: 34dbb99afe9d0774ba685b3ff21205e7";
                        }
                    }

                    return match.Value;
                }, RegexOptions.Multiline | RegexOptions.IgnoreCase);

                File.WriteAllText(file.FullName, content);

                Console.WriteLine($"{file.Name} processed");
            }
        }
    }

    private class Remap
    {
        public static Dictionary<string, Remap> types;

        public string? __label__;
        public string? guid;
        public string? id;
        public string? className;
    }
}