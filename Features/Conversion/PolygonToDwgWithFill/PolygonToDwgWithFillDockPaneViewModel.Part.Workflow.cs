using System;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.Conversion.PolygonToDwgWithFill.Core;
using XIAOFUTools.Shared.IO.Cad;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Conversion.PolygonToDwgWithFill
{
    internal partial class PolygonToDwgWithFillDockPaneViewModel
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
                        StatusMessage = "正在生成DWG...";
                    });
                    await ExecuteAsync();
                }
                catch (Exception exception)
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        StatusMessage = $"执行失败: {exception.Message}";
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
                SetStatus("请选择输出DWG路径");
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
                SetStatus("所选图层无有效面要素");
                return;
            }

            var options = new DwgFillExportOptions(
                SelectedDwgVersion?.Version ?? DwgExportVersion.AutoCad2018,
                ExportBoundary,
                ExportHatch,
                LineWidth,
                HatchTransparency);
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                IsProgressIndeterminate = false;
                Progress = 0;
                StatusMessage = $"开始写入DWG，共 {polygons.Count} 个要素...";
            });

            var result = await _documentWriter.WriteAsync(
                polygons,
                outputPath,
                options,
                () => CancelRequested,
                progress => PresentationServices.UiThread.Post(() =>
                {
                    Progress = (int)Math.Round(100.0 * progress.ProcessedCount / progress.TotalCount);
                    StatusMessage = $"正在写入DWG: {progress.ProcessedCount}/{progress.TotalCount}";
                }));

            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                StatusMessage = result.Cancelled ? "已取消" : $"已生成DWG：{result.OutputPath}";
                LogInfo(StatusMessage);
            });
        }

        private void SetStatus(string message)
        {
            PresentationServices.UiThread.InvokeOrRun(() => StatusMessage = message);
        }
    }
}
