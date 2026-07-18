using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Features.DataManagement.DatabaseBuilder.Infrastructure
{
    /// <summary>
    /// 负责建库模板和本地输入、输出路径的文件系统操作。
    /// </summary>
    internal sealed class DatabaseBuilderFileStore
    {
        private static readonly string[] TemplateFileNames =
        {
            "建库模板.xls",
            "建库模板-带要素集.xls"
        };

        internal bool FileExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && File.Exists(path);

        internal bool DirectoryExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

        internal IReadOnlyList<DatabaseBuilderTemplateFile> GetAvailableTemplates(string addinFolder)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(addinFolder);

            return TemplateFileNames
                .Select(fileName => new DatabaseBuilderTemplateFile(
                    fileName,
                    Path.Combine(addinFolder, "Data", "Excel模板", fileName)))
                .Where(template => File.Exists(template.SourcePath))
                .ToArray();
        }

        internal void CopyTemplates(IEnumerable<DatabaseBuilderTemplateFile> templates, string outputFolder)
        {
            ArgumentNullException.ThrowIfNull(templates);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);

            foreach (DatabaseBuilderTemplateFile template in templates)
            {
                File.Copy(template.SourcePath, Path.Combine(outputFolder, template.FileName), overwrite: true);
            }
        }
    }

    internal sealed record DatabaseBuilderTemplateFile(string FileName, string SourcePath);
}
