using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Features.Conversion.SpecialCoordinateTransform.Infrastructure
{
    /// <summary>
    /// 负责特殊坐标转换批处理的本地目录扫描和工作空间目录生命周期。
    /// 不持有 ArcGIS 数据集对象，调用方仍负责在正确的 ArcGIS 任务上下文中使用数据集。
    /// </summary>
    internal sealed class SpecialCoordinateTransformFileStore
    {
        internal bool DirectoryExists(string folderPath) =>
            !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);

        internal void EnsureDirectory(string folderPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
            Directory.CreateDirectory(folderPath);
        }

        internal void DeleteDirectoryIfExists(string folderPath, bool recursive)
        {
            if (DirectoryExists(folderPath))
            {
                Directory.Delete(folderPath, recursive);
            }
        }

        internal IReadOnlyList<string> EnumeratePaths(
            string rootFolder,
            bool includeSubfolders,
            string searchPattern,
            bool searchDirectories)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootFolder);
            ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);
            var searchOption = includeSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;
            IEnumerable<string> paths = searchDirectories
                ? Directory.EnumerateDirectories(rootFolder, searchPattern, searchOption)
                : Directory.EnumerateFiles(rootFolder, searchPattern, searchOption);

            return paths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
