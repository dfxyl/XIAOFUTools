using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.Conversion.PolygonToDxfWithFill.Core;
using XIAOFUTools.Features.Conversion.PolygonToDxfWithFill.Infrastructure;
using XIAOFUTools.Shared.IO.Cad;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Conversion.PolygonToDxfWithFill
{
    internal partial class PolygonToDxfWithFillDockPaneViewModel
    {
        private void ExecuteAsyncSafe()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        IsProcessing = true;
                        Progress = 0;
                        IsProgressIndeterminate = true;
                        CancelRequested = false;
                        StatusMessage = "正在生成DXF...";
                    });

                    await ExecuteAsync();
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        StatusMessage = $"执行失败: {ex.Message}";
                        LogError(StatusMessage);
                    });
                }
                finally
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        IsProcessing = false;
                        IsProgressIndeterminate = false;
                        Progress = CancelRequested ? 0 : 100;
                        NotifyPropertyChanged(nameof(CanProcess));
                    });
                }
            });
        }

        private async Task ExecuteAsync()
        {
            var inputLayer = SelectedPolygonLayer;
            var outputPath = OutputPath;
            if (inputLayer == null)
            {
                SetStatus("请选择面图层");
                return;
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                SetStatus("请选择输出DXF路径");
                return;
            }

            CadPolygonReadOptions readOptions = null;
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                readOptions = new CadPolygonReadOptions(
                    UseFieldNaming,
                    FieldNamingSeparator,
                    NamingFields.Where(field => field.IsSelected).Select(field => field.Name).ToArray());
            });
            var polygons = await _polygonReader.ReadAsync(inputLayer, readOptions);
            if (polygons.Count == 0)
            {
                SetStatus("未提取到任何面要素");
                return;
            }

            var options = new DxfFillExportOptions(
                SelectedDxfVersion?.Version ?? DxfExportVersion.AutoCad2018,
                ExportBoundary,
                ExportHatch,
                LineWidth,
                HatchTransparency);
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                IsProgressIndeterminate = false;
                Progress = 0;
                StatusMessage = $"开始写入DXF，共 {polygons.Count} 个要素...";
            });

            var result = await _documentWriter.WriteAsync(
                polygons,
                outputPath,
                options,
                () => CancelRequested,
                progress => PresentationServices.UiThread.Post(() =>
                {
                    Progress = (int)Math.Round(100.0 * progress.ProcessedCount / progress.TotalCount);
                    StatusMessage = $"正在写入DXF: {progress.ProcessedCount}/{progress.TotalCount}";
                }));

            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                StatusMessage = result.Cancelled ? "已取消" : $"完成: {result.OutputPath}";
                LogInfo(StatusMessage);
            });
        }

        private void SetStatus(string message)
        {
            PresentationServices.UiThread.InvokeOrRun(() => StatusMessage = message);
        }
    }
}
