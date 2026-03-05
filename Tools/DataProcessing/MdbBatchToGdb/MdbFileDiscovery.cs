using System;
using System.Collections.Generic;
using System.IO;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal static class MdbFileDiscovery
    {
        public static List<string> EnumerateMdbFiles(string rootPath, bool includeSubfolders)
        {
            var results = new List<string>();
            var pending = new Stack<string>();
            pending.Push(rootPath);

            while (pending.Count > 0)
            {
                string current = pending.Pop();

                try
                {
                    results.AddRange(Directory.EnumerateFiles(current, "*.mdb", SearchOption.TopDirectoryOnly));
                }
                catch
                {
                }

                if (!includeSubfolders)
                {
                    continue;
                }

                try
                {
                    foreach (string subDirectory in Directory.EnumerateDirectories(current))
                    {
                        pending.Push(subDirectory);
                    }
                }
                catch
                {
                }
            }

            return results;
        }

        public static string BuildRelativePath(string rootPath, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return fullPath;
            }

            string normalizedRoot = Path.GetFullPath(rootPath);
            string normalizedFullPath = Path.GetFullPath(fullPath);

            if (!normalizedFullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            string relative = normalizedFullPath.Substring(normalizedRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return string.IsNullOrWhiteSpace(relative)
                ? Path.GetFileName(fullPath)
                : relative;
        }
    }
}
