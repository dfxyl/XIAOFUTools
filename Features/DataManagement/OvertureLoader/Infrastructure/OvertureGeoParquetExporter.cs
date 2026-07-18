using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure
{
    internal sealed class OvertureGeoParquetExporter : IOvertureGeoParquetExporter
    {
        private readonly IOvertureDuckDbDataSession _dataSession;
        private readonly IOvertureMapMemberCleanupService _mapCleanupService;

        public OvertureGeoParquetExporter(
            IOvertureDuckDbDataSession dataSession,
            IOvertureMapMemberCleanupService mapCleanupService)
        {
            _dataSession = dataSession ?? throw new ArgumentNullException(nameof(dataSession));
            _mapCleanupService = mapCleanupService ??
                throw new ArgumentNullException(nameof(mapCleanupService));
        }

        public async Task<string> ExportAsync(
            string selectQuery,
            string outputPath,
            string layerName,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report($"正在将 {layerName} 的数据导出到 {Path.GetFileName(outputPath)}");
            var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("无法解析 GeoParquet 输出目录。");
            }
            Directory.CreateDirectory(outputDirectory);

            if (File.Exists(outputPath))
            {
                await _mapCleanupService.RemoveFromProjectMapsUsingFileAsync(
                    outputPath,
                    cancellationToken).ConfigureAwait(false);
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                await TryDeleteExistingFileAsync(outputPath, cancellationToken).ConfigureAwait(false);
            }

            var actualOutputPath = File.Exists(outputPath)
                ? $"{outputPath}.tmp_{DateTime.Now:yyyyMMdd_HHmmss_fff}"
                : outputPath;
            await _dataSession.ExportGeoParquetAsync(
                selectQuery,
                actualOutputPath,
                cancellationToken).ConfigureAwait(false);

            if (!string.Equals(actualOutputPath, outputPath, StringComparison.OrdinalIgnoreCase))
            {
                outputPath = await ReplaceOriginalAsync(
                    actualOutputPath,
                    outputPath,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!File.Exists(outputPath))
            {
                throw new FileNotFoundException("DuckDB 导出完成，但未找到 GeoParquet 文件。", outputPath);
            }

            progress?.Report($"Successfully exported data for {layerName}.");
            return outputPath;
        }

        private static async Task TryDeleteExistingFileAsync(
            string outputPath,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Delete(outputPath);
                    return;
                }
                catch (IOException) when (attempt < 2)
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    return;
                }
            }
        }

        private static async Task<string> ReplaceOriginalAsync(
            string temporaryPath,
            string outputPath,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                    File.Move(temporaryPath, outputPath);
                    return outputPath;
                }
                catch (IOException) when (attempt < 3)
                {
                    await Task.Delay(attempt * 100, cancellationToken).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    return temporaryPath;
                }
            }

            return temporaryPath;
        }
    }
}
