using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;

Console.WriteLine("Setting up the Unity editor for Shadows of Doubt");

string gamePath = "";
string editorPath = "";

if(args.Length == 2)
{
    gamePath = args[0];
    editorPath = args[1];
}
else
{
    Console.Write("Provide the path to the game:");
    gamePath = Console.ReadLine();
    Console.Write("Provide the path where you want the editor (Will be emptied):");
    editorPath = Console.ReadLine();
}

if (!Directory.Exists(gamePath) || !Directory.Exists(editorPath)) throw new IOException("Folders not found");

Helpers.listAllProcessedFiles = true;

string projectPath = Path.Join(editorPath, "ExportedProject");
string assetsPath = Path.Join(projectPath, "Assets");
string modContentPath = Path.Join(assetsPath, "_ModContent");
string gameExtractPath = Path.Join(assetsPath, "GameExtract");

Console.WriteLine($"GameDir {gamePath} : EditorDir {editorPath}");

// BuildAssetMap.Build(gamePath);

// Rip the entire project from the game itself
Console.WriteLine("Ripping project...");

var proc = new System.Diagnostics.Process
{
    StartInfo = new System.Diagnostics.ProcessStartInfo
    {
        FileName = @"./Tools/AssetRipper/Debug/AssetRipper.exe",
        Arguments = $"\"{gamePath}\" -o \"{editorPath}\" -q",
        UseShellExecute = false
    }
};

proc.Start();
proc.WaitForExit();

Console.WriteLine("Project ripped");

string pathIdMapPath = Path.Join(editorPath, "AuxiliaryFiles", "path_id_map.json");

Console.WriteLine("Processing references...");

Helpers.RepointGUIDs(pathIdMapPath, new DirectoryInfo(assetsPath));

Dictionary<(string, string), (long, string)> addressablesMap = new Dictionary<(string, string), (long, string)>();

JObject o1 = JObject.Parse(File.ReadAllText(pathIdMapPath));
foreach(var file in o1["Files"])
{
    if (file["Name"].Value<string>().StartsWith("cab"))
    {
        foreach(var asset in file["Assets"])
        {
            addressablesMap[(asset["Type"].Value<string>(), asset["Name"].Value<string>())] = (asset["PathID"].Value<long>(), file["Name"].Value<string>());
        }
    }
}

foreach (var file in o1["Files"])
{
    if (!file["Name"].Value<string>().StartsWith("cab"))
    {
        foreach (var asset in file["Assets"])
        {
            var key = (asset["Type"].Value<string>(), asset["Name"].Value<string>());
            if (addressablesMap.ContainsKey(key))
            {
                asset["AddressablePathID"] = addressablesMap[key].Item1;
                asset["AddressablesCAB"] = addressablesMap[key].Item2;
            }
        }
    }
}

File.WriteAllText(pathIdMapPath, o1.ToString());


Console.WriteLine("References processed");

// Create a mapping for Texture2D GUIDs, as Unity will nuke them when compressing
BackupTexture2DGUIDs.CreateBackup(Path.Join(assetsPath, "Texture2D"), Path.Join(projectPath, "Texture2DGUIDs.json"));

// Moving a bunch of files around inside the project, and deleting some that would cause issues
Console.WriteLine("Adjusting project layout...");

Directory.CreateDirectory(modContentPath);
Directory.CreateDirectory(gameExtractPath);

string[] directoriesToStay = new string[] { "_ModContent", "GameExtract", "Dlls", "HDRPDefaultResources", "LightingSettings" };
string[] directoriesToRemove = new string[] { "Scripts", "Plugins", "LightingDataAsset", "Scenes", "StreamingAssets", "Shader" };

foreach(var directory in Directory.EnumerateDirectories(assetsPath))
{
    var directoryInfo = new DirectoryInfo(directory);

    if(directoriesToRemove.Contains(directoryInfo.Name))
    {
        directoryInfo.Delete(true);
        continue;
    }
    else if (directoriesToStay.Contains(directoryInfo.Name))
    {
        continue;
    }
    else
    {
        Helpers.CopyFolder(directoryInfo, new DirectoryInfo(gameExtractPath), true, false);
    }
}

Console.WriteLine("Moving ScriptableObjects");

var soDirectory = Directory.CreateDirectory(Path.Join(assetsPath, "GameExtract", "ScriptableObjects"));
var soFiles = new DirectoryInfo(assetsPath).GetFiles("*.asset");
for (int i = soFiles.Length - 1; i >= 0; i--)
{
    File.Move(soFiles[i].FullName, Path.Join(soDirectory.FullName, soFiles[i].Name));
    File.Move(soFiles[i].FullName + ".meta", Path.Join(soDirectory.FullName, soFiles[i].Name) + ".meta");
}

var jsonDirectory = Directory.CreateDirectory(Path.Join(assetsPath, "GameExtract", "JSON"));
var jsonFiles = new DirectoryInfo(assetsPath).GetFiles("*.json");
for (int i = jsonFiles.Length - 1; i >= 0; i--)
{
    File.Move(jsonFiles[i].FullName, Path.Join(jsonDirectory.FullName, jsonFiles[i].Name));
    File.Move(jsonFiles[i].FullName + ".meta", Path.Join(jsonDirectory.FullName, jsonFiles[i].Name) + ".meta");
}

Console.WriteLine("Project layout adjusted");

// Project template, basically the rest of the UnityProject required to make it all work
Console.WriteLine("Installing the project template...");
foreach (var directory in Directory.EnumerateDirectories("./Tools/ProjectTemplate"))
{
    Helpers.CopyFolder(new DirectoryInfo(directory), new DirectoryInfo(projectPath), true, true);
}
foreach(var file in Directory.EnumerateFiles("./Tools/ProjectTemplate"))
{
    File.Copy(file, Path.Join(projectPath, new FileInfo(file).Name), true);
}

// Create a config file for the editor to use later
string editorSettingsPath = Path.Join(assetsPath, "EditorSettings.asset");
var editorSettings = File.ReadAllLines(editorSettingsPath);
for(int i = 0; i < editorSettings.Length; i++)
{
    if (editorSettings[i].Contains("GamePath:")) editorSettings[i] += gamePath;
}
File.WriteAllLines(editorSettingsPath, editorSettings);

Console.WriteLine("Project template installed");

// Move the path_id_map into somewhere people won't nuke it on accident
File.Copy(pathIdMapPath, Path.Join(projectPath, "path_id_map.json"));

// Rename the project
Directory.Move(projectPath, Path.Join(editorPath, "ShadowsOfDoubtEditor"));

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Done, press enter to quit");
Console.ReadLine();