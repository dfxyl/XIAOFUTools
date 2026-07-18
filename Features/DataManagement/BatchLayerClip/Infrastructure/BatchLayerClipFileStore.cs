using System;
using System.IO;

namespace XIAOFUTools.Features.DataManagement.BatchLayerClip.Infrastructure
{
    /// <summary>
    /// 负责批量裁剪输出目录的可用性校验与创建。
    /// </summary>
    internal sealed class BatchLayerClipFileStore
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
