using System;
using System.Collections.Generic;
using System.IO;

namespace XIAOFUTools.Features.DataManagement.BatchMergeShp.Infrastructure
{
    /// <summary>
    /// 负责批量合并 SHP 的本地目录校验、扫描和输出目录准备。
    /// </summary>
    internal sealed class BatchMergeShpFileStore
    {
        internal bool DirectoryExists(string folderPath) =>
            !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);

        internal void EnsureDirectory(string folderPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
            Directory.CreateDirectory(folderPath);
        }

        internal IReadOnlyList<string> EnumerateShapefiles(string rootPath, bool includeSubfolders)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            var results = new List<string>();
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootPath);

            while (pendingDirectories.Count > 0)
            {
                var currentDirectory = pendingDirectories.Pop();
                try
                {
                    results.AddRange(Directory.EnumerateFiles(
                        currentDirectory,
                        "*.shp",
                        SearchOption.TopDirectoryOnly));
                }
                catch
                {
                    // 忽略不可访问目录，保持原有尽力扫描行为。
                }

                if (!includeSubfolders)
                {
                    continue;
                }

                try
                {
                    foreach (var directory in Directory.EnumerateDirectories(currentDirectory))
                    {
                        pendingDirectories.Push(directory);
                    }
                }
                catch
                {
                    // 忽略不可访问目录，保持原有尽力扫描行为。
                }
            }

            return results;
        }
    }
}
