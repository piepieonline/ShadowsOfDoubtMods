using System.IO;

namespace AssetBundleLoader
{
    public static class ModFilePath
    {
        /// The game runs under Wine on Linux, so mod paths are Windows paths either way, and \\?\ bypasses MAX_PATH
        public static string Resolve(string filePath)
        {
            if (File.Exists("\\\\?\\" + filePath)) return "\\\\?\\" + filePath;
            if (File.Exists(filePath)) return filePath;
            return null;
        }
        
        public static string CacheKey(string filePath)
        {
            string plainPath = filePath.StartsWith("\\\\?\\") ? filePath.Substring(4) : filePath;

            try
            {
                plainPath = Path.GetFullPath(plainPath);
            }
            catch
            {
                // A path the runtime won't normalise still keys fine as it was written
            }

            return plainPath.Replace('\\', '/').ToLowerInvariant();
        }
    }
}
