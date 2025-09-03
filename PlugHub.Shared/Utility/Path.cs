using System.Runtime.InteropServices;

namespace PlugHub.Shared.Utility
{
    public static class PathUtilities
    {
        public static bool EqualsPath(string path1, string path2)
        {
            if (path1 == null || path2 == null)
                return false;

            string fullPath1 = Path.GetFullPath(path1).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath2 = Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return fullPath1.Equals(fullPath2,
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }
        public static bool ExistsOsAware(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                string normalizedPath = Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                return File.Exists(normalizedPath);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}