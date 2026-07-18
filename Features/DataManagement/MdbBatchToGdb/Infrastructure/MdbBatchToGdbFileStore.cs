using System;
using System.Collections.Generic;
using System.IO;

namespace XIAOFUTools.Features.DataManagement.MdbBatchToGdb.Infrastructure
{
    /// <summary>
    /// 负责 MDB 批量转换所需的本地文件系统操作。
    /// </summary>
    internal sealed class MdbBatchToGdbFileStore
    {
        internal bool DirectoryExists(string folderPath) =>
            !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);

        internal bool PathExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && (Directory.Exists(path) || File.Exists(path));

        internal void EnsureDirectory(string folderPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
            Directory.CreateDirectory(folderPath);
        }

        internal void DeletePath(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return;
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        internal List<string> EnumerateMdbFiles(string rootPath, bool includeSubfolders)
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
                catch (UnauthorizedAccessException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }
                catch (IOException)
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
                catch (UnauthorizedAccessException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }
                catch (IOException)
                {
                }
            }

            return results;
        }

        internal string BuildRelativePath(string rootPath, string fullPath)
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
