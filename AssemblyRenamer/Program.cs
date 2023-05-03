
/*using Mono.Cecil;


var oldName = "Assembly-CSharp";
// var newName = "Assembly-CSharp";
var newName = "GameDll";

// var assemblyReference = AssemblyDefinition.ReadAssembly($"D:\\Game Modding\\ShadowsOfDoubt\\IL2CppDumper\\DummyDll\\{oldName}.dll");
var assemblyReference = AssemblyDefinition.ReadAssembly($"E:\\SteamLibrary\\steamapps\\common\\Shadows of Doubt\\BepInEx\\plugins\\DebugMod\\DebugMod.dll");


foreach(var asmRef in assemblyReference.MainModule.AssemblyReferences)
{
    if(asmRef.Name == oldName)
    { 
        asmRef.Name = newName;
        break;
    }
}

assemblyReference.Write($"D:\\UnityDev\\ShadowsOfDoubtModding\\Assets\\Dlls\\DebugMod.dll");
*/

// Min working for modifying the game's dll file
using Mono.Cecil;

var oldName = "Assembly-CSharp";
var newName = "GameDll";

// Good lib, includes some source
// var assemblyReference = AssemblyDefinition.ReadAssembly($"D:\\Game Modding\\ShadowsOfDoubt\\Cpp2IL\\cpp2il_out\\{oldName}.dll");

// Hollow lib only
var assemblyReference = AssemblyDefinition.ReadAssembly($"D:\\Game Modding\\ShadowsOfDoubt\\IL2CppDumper\\DummyDll\\{oldName}.dll");


assemblyReference.Name = new AssemblyNameDefinition(newName, assemblyReference.Name.Version);
assemblyReference.MainModule.Types[0].Namespace = "ColePowered";

assemblyReference.Write($"D:\\UnityDev\\ShadowsOfDoubtModding\\Assets\\Dlls\\{newName}.dll");
