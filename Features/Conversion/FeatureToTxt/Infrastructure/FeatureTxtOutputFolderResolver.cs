using System;
using System.IO;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt.Infrastructure
{
    internal sealed record FeatureTxtOutputFolderResolution(string Path, string Source);

    /// <summary>
    /// 集中处理 TXT 导出目录的可用性检查与默认目录回退。
    /// </summary>
    internal sealed class FeatureTxtOutputFolderResolver
    {
        internal bool DirectoryExists(string folderPath) =>
            !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);

        internal FeatureTxtOutputFolderResolution ResolveDefaultOutputFolder(string projectPath)
        {
            var projectDirectory = string.IsNullOrWhiteSpace(projectPath)
                ? null
                : Path.GetDirectoryName(projectPath);
            if (DirectoryExists(projectDirectory))
            {
                return new FeatureTxtOutputFolderResolution(projectDirectory, "工程位置");
            }

            var documentsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (DirectoryExists(documentsDirectory))
            {
                return new FeatureTxtOutputFolderResolution(documentsDirectory, "文档文件夹");
            }

            var desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            return DirectoryExists(desktopDirectory)
                ? new FeatureTxtOutputFolderResolution(desktopDirectory, "桌面")
                : null;
        }
    }
}
