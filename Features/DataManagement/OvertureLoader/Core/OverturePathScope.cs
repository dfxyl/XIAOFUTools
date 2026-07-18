using System;
using System.IO;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Core
{
    internal static class OverturePathScope
    {
        private const string ParquetExtension = ".parquet";

        public static bool IsWithinFolder(string candidatePath, string folderPath)
        {
            if (!TryNormalize(candidatePath, out var candidate) ||
                !TryNormalize(folderPath, out var folder))
            {
                return false;
            }

            if (string.Equals(candidate, folder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var folderPrefix = folder + Path.DirectorySeparatorChar;
            return candidate.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool RefersToFile(string dataSourcePath, string targetFilePath)
        {
            if (!TryNormalizeParquetDataSource(dataSourcePath, out var sourceFile) ||
                !TryNormalize(targetFilePath, out var targetFile))
            {
                return false;
            }

            return string.Equals(sourceFile, targetFile, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryNormalizeParquetDataSource(string path, out string normalizedPath)
        {
            normalizedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var parquetIndex = path.IndexOf(ParquetExtension, StringComparison.OrdinalIgnoreCase);
            if (parquetIndex >= 0)
            {
                var extensionEnd = parquetIndex + ParquetExtension.Length;
                if (extensionEnd == path.Length ||
                    path[extensionEnd] == Path.DirectorySeparatorChar ||
                    path[extensionEnd] == Path.AltDirectorySeparatorChar)
                {
                    path = path[..extensionEnd];
                }
            }

            return TryNormalize(path, out normalizedPath);
        }

        private static bool TryNormalize(string path, out string normalizedPath)
        {
            normalizedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                normalizedPath = Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return normalizedPath.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
