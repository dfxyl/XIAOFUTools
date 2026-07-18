using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.Cartography.ExportLayout.Infrastructure
{
    /// <summary>
    /// 负责布局导出目标目录的本地文件系统准备。
    /// </summary>
    internal sealed class LayoutExportFileStore
    {
        internal Task EnsureOutputDirectoryAsync(string folderPath, CancellationToken cancellationToken) =>
            Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
                Directory.CreateDirectory(folderPath);
            }, cancellationToken);
    }
}
