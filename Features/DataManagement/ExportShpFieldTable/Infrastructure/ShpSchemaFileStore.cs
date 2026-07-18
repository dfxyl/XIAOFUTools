using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Features.DataManagement.ExportShpFieldTable.Infrastructure
{
    /// <summary>
    /// 负责 SHP 字段表导出的本地目录与文件发现。
    /// </summary>
    internal sealed class ShpSchemaFileStore
    {
        internal bool DirectoryExists(string folderPath) =>
            !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);

        internal IReadOnlyList<string> GetTopLevelShapefiles(string folderPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
            return Directory.EnumerateFiles(folderPath, "*.shp", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal void EnsureOutputDirectory(string outputFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
            var outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
        }
    }
}
