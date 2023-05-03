Console.WriteLine("Setting up the Unity editor for Shadows of Doubt");
Console.Write("Provide the path to the game:");
string gamePath = Console.ReadLine() ?? "";
// string gamePath = "E:\\SteamLibrary\\steamapps\\common\\Shadows of Doubt";

Console.Write("Provide the path where you want the editor (Will be emptied):");
string editorPath = Console.ReadLine() ?? "";
// string editorPath = "D:\\Game Modding\\ShadowsOfDoubt\\ShadowsOfDoubtEditor"; // Console.ReadLine() ?? "";

if (!Directory.Exists(gamePath) || !Directory.Exists(editorPath)) throw new IOException("Folders not found");

string projectPath = Path.Join(editorPath, "ExportedProject");
string assetsPath = Path.Join(projectPath, "Assets");
string modContentPath = Path.Join(assetsPath, "_ModContent");
string gameExtractPath = Path.Join(assetsPath, "GameExtract");

Console.WriteLine($"GameDir {gamePath} : EditorDir {editorPath}");

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

// Moving a bunch of files around inside the project, and deleting some that would cause issues
Console.WriteLine("Adjusting project layout...");

Directory.CreateDirectory(modContentPath);
Directory.CreateDirectory(gameExtractPath);

string[] directoriesToStay = new string[] { "_ModContent", "GameExtract", "Dlls", "HDRPDefaultResources", "LightingSettings" };
string[] directoriesToRemove = new string[] { "Scripts", "Plugins", "LightingDataAsset", "StreamingAssets", "Scenes" };

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

Console.WriteLine("Project layout adjusted");

// Project template, basically the rest of the UnityProject required to make it all work
Console.WriteLine("Installing the project template...");
foreach (var directory in Directory.EnumerateDirectories("./Tools/ProjectTemplate"))
{
    Helpers.CopyFolder(new DirectoryInfo(directory), new DirectoryInfo(projectPath), true, true);
}

Console.WriteLine("Project template installed");

Console.WriteLine("Processing references...");
Helpers.RepointGUIDs(new DirectoryInfo(gameExtractPath));
Console.WriteLine("References processed");

// Move the path_id_map into somewhere people won't nuke it on accident
File.Copy(Path.Join(editorPath, "AuxiliaryFiles", "path_id_map.json"), Path.Join(projectPath, "path_id_map.json"));

// Create a config file for the editor to use later
File.WriteAllText(Path.Join(projectPath, "config.json"), "{ \"gameInstallationFolder\": \"" + gamePath + "\", \"modName\": \"\" }");

// Rename the project
Directory.Move(projectPath, Path.Join(editorPath, "ShadowsOfDoubtEditor"));

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Done, press enter to quit");
Console.ReadLine();