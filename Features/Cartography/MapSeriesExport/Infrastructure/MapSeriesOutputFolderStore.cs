using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport.Infrastructure
{
    /// <summary>
    /// 负责地图系列导出目标目录的创建与取消检查。
    /// </summary>
    internal sealed class MapSeriesOutputFolderStore
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
