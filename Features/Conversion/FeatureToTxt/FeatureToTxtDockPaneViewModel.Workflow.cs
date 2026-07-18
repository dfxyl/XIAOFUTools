using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.Conversion.FeatureToTxt.Core;
using XIAOFUTools.Features.Conversion.FeatureToTxt.Infrastructure;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    internal partial class FeatureToTxtDockPaneViewModel
    {
        private readonly ArcGisFeatureExportReader _featureExportReader = new();
        private readonly FeatureSnapshotTextFormatter _snapshotFormatter = new();
        private readonly FeatureTxtOutputFolderResolver _outputFolderResolver = new();
        private CancellationTokenSource _executionCancellation;

        private void ExecuteAsyncSafe()
        {
            _ = ExecuteAsync();
        }

        private async Task ExecuteAsync()
        {
            if (SelectedPolygonLayer == null)
            {
                StatusMessage = "请选择面图层。";
                return;
            }

            if (!_outputFolderResolver.DirectoryExists(OutputPath))
            {
                StatusMessage = "请选择有效的输出文件夹。";
                return;
            }

            _executionCancellation?.Cancel();
            _executionCancellation?.Dispose();
            _executionCancellation = new CancellationTokenSource();
            var cancellationToken = _executionCancellation.Token;
            var layer = SelectedPolygonLayer;
            var layerName = layer.Name;
            var outputPath = OutputPath;
            var header = GenerateFileHeader();
            var configuration = CreateExportConfiguration();
            var exportSeparately = ExportSeparately;
            var groupByField = GroupByField;
            var groupByFieldName = GroupByFieldName;

            IsProcessing = true;
            CancelRequested = false;
            Progress = 0;
            IsProgressIndeterminate = true;
            StatusMessage = "正在读取要素快照...";
            LogContent = string.Empty;

            try
            {
                LogInfo($"开始转换 - 图层: {layerName}", true);
                LogInfo($"输出文件夹: {outputPath}", true);
                var readResult = await _featureExportReader.ReadAsync(
                    layer,
                    UseSelection,
                    cancellationToken);

                foreach (var warning in readResult.Warnings)
                {
                    LogWarning(warning);
                }

                if (readResult.Snapshots.Count == 0)
                {
                    LogWarning($"读取 {readResult.SourceCount} 个要素，但没有可导出的有效面几何。");
                    StatusMessage = "没有可导出的有效面要素。";
                    return;
                }

                LogInfo(
                    $"已读取 {readResult.SourceCount} 个要素，生成 {readResult.Snapshots.Count} 个纯数据快照。",
                    true);
                IsProgressIndeterminate = false;
                StatusMessage = "正在生成文本文件...";

                var result = await Task.Run(
                    () => ExportSnapshots(
                        readResult.Snapshots,
                        layerName,
                        outputPath,
                        header,
                        configuration,
                        exportSeparately,
                        groupByField,
                        groupByFieldName,
                        cancellationToken),
                    cancellationToken);

                Progress = 100;
                StatusMessage = $"转换完成，导出 {result.FeatureCount} 个要素、{result.FileCount} 个文件。";
                LogInfo(StatusMessage, true);
            }
            catch (OperationCanceledException)
            {
                CancelRequested = true;
                StatusMessage = "操作已取消。";
                LogWarning(StatusMessage);
            }
            catch (Exception ex)
            {
                StatusMessage = $"转换失败: {ex.Message}";
                LogError(StatusMessage);
                LogError($"异常详情: {ex}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                _executionCancellation?.Dispose();
                _executionCancellation = null;
            }
        }

        private FeatureExportWorkflowResult ExportSnapshots(
            IReadOnlyList<FeatureExportSnapshot> snapshots,
            string layerName,
            string outputPath,
            string header,
            FeatureToTxtExportConfiguration configuration,
            bool exportSeparately,
            bool groupByField,
            string groupByFieldName,
            CancellationToken cancellationToken)
        {
            if (groupByField && !string.IsNullOrWhiteSpace(groupByFieldName))
            {
                return ExportGroupedSnapshots(
                    snapshots,
                    outputPath,
                    header,
                    configuration,
                    groupByFieldName,
                    cancellationToken);
            }

            if (exportSeparately)
            {
                var exported = 0;
                for (var index = 0; index < snapshots.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileName = GetSafeFileName($"{layerName}_{index + 1:D6}");
                    var content = BuildSingleContent(
                        snapshots[index],
                        header,
                        configuration,
                        index + 1,
                        cancellationToken);
                    SaveToFileSync(content, Path.Combine(outputPath, $"{fileName}.txt"));
                    exported++;
                    ReportExportProgress(exported, snapshots.Count);
                }

                return new FeatureExportWorkflowResult(exported, exported);
            }

            var mergedContent = BuildContent(snapshots, header, configuration, cancellationToken);
            var mergedFileName = GetSafeFileName(layerName);
            SaveToFileSync(mergedContent, Path.Combine(outputPath, $"{mergedFileName}.txt"));
            ReportExportProgress(snapshots.Count, snapshots.Count);
            return new FeatureExportWorkflowResult(snapshots.Count, 1);
        }

        private FeatureExportWorkflowResult ExportGroupedSnapshots(
            IReadOnlyList<FeatureExportSnapshot> snapshots,
            string outputPath,
            string header,
            FeatureToTxtExportConfiguration configuration,
            string groupByFieldName,
            CancellationToken cancellationToken)
        {
            var groups = snapshots
                .GroupBy(snapshot =>
                {
                    var value = snapshot.GetFieldValue(groupByFieldName);
                    return string.IsNullOrWhiteSpace(value) ? "未知值" : value;
                })
                .ToList();
            var processed = 0;
            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var groupSnapshots = group.ToList();
                var content = BuildContent(
                    groupSnapshots,
                    header,
                    configuration,
                    cancellationToken);
                var fileName = GetSafeFileName(group.Key);
                SaveToFileSync(content, Path.Combine(outputPath, $"{fileName}.txt"));
                processed += groupSnapshots.Count;
                ReportExportProgress(processed, snapshots.Count);
            }

            return new FeatureExportWorkflowResult(processed, groups.Count);
        }

        private string BuildContent(
            IReadOnlyList<FeatureExportSnapshot> snapshots,
            string header,
            FeatureToTxtExportConfiguration configuration,
            CancellationToken cancellationToken)
        {
            var content = new StringBuilder();
            content.AppendLine(header);
            for (var index = 0; index < snapshots.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _snapshotFormatter.Write(snapshots[index], content, index + 1, configuration);
            }

            return content.ToString();
        }

        private string BuildSingleContent(
            FeatureExportSnapshot snapshot,
            string header,
            FeatureToTxtExportConfiguration configuration,
            int featureIndex,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = new StringBuilder();
            content.AppendLine(header);
            _snapshotFormatter.Write(snapshot, content, featureIndex, configuration);
            return content.ToString();
        }

        private void ReportExportProgress(int processed, int total)
        {
            if (processed % 100 != 0 && processed != total)
            {
                return;
            }

            var progress = total == 0 ? 0 : (int)((double)processed / total * 100);
            PresentationServices.UiThread.PostBackground(() =>
            {
                Progress = progress;
                StatusMessage = $"已处理 {processed}/{total} 个要素";
            });
        }

        private sealed record FeatureExportWorkflowResult(int FeatureCount, int FileCount);
    }
}
