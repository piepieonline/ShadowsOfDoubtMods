using System.Text;

Console.Write("City (1) or Save (2)");
var isCity = (Console.ReadLine() == "1");
var dir = isCity ? "Cities" : "Save";
var fileType = isCity ? "cit" : "sod";

Console.Write("Name of city to decompress:");
var cityFileName = Console.ReadLine();

var loadedBytes = File.ReadAllBytes($"C:\\Users\\Thomas\\appdata\\locallow\\ColePowered Games\\Shadows of Doubt\\{dir}\\{cityFileName}.{fileType}b");

byte[] bytes = brotli.decompressBuffer(loadedBytes, true);
if (bytes != null)
{
    var jsonString = Encoding.UTF8.GetString(bytes);

    File.WriteAllText($"C:\\Users\\Thomas\\appdata\\locallow\\ColePowered Games\\Shadows of Doubt\\{dir}\\{cityFileName}.{fileType}.exported", jsonString);
}