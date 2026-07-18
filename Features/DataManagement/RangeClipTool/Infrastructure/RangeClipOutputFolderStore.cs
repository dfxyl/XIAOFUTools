using System;
using System.IO;

namespace XIAOFUTools.Features.DataManagement.RangeClipTool.Infrastructure
{
    /// <summary>
    /// 负责范围裁剪输出目录的验证与创建。
    /// </summary>
    internal sealed class RangeClipOutputFolderStore
    {
        internal bool DirectoryExists(string folderPath) =>
            !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);

        internal void EnsureDirectory(string folderPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
            Directory.CreateDirectory(folderPath);
        }
    }
}
