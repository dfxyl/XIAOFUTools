using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Cartography.ExportLayout
{
    public partial class ExportLayoutViewModel
    {

        /// <summary>
        /// 开始导出
        /// </summary>
        private async Task StartExport()
        {
            try
            {
                IsRunning = true;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                await _fileStore.EnsureOutputDirectoryAsync(OutputFolder, _cancellationTokenSource.Token);

                var selectedLayouts = Layouts.Where(l => l.IsSelected).ToList();
                int totalCount = selectedLayouts.Count;
                int currentCount = 0;

                foreach (var layoutItem in selectedLayouts)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    currentCount++;
                    
                    await QueuedTask.Run(() =>
                    {
                        try
                        {
                            ExportLayout(layoutItem.Name);
                        }
                        catch (Exception ex)
                        {
                            PresentationServices.UiThread.InvokeOrRun(() =>
                            {
                                PresentationServices.Dialogs.Show($"导出布局 '{layoutItem.Name}' 时发生错误：{ex.Message}",
                                              "导出错误");
                            });
                        }
                    });
                }

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    PresentationServices.Dialogs.Show($"导出完成！共导出 {currentCount} 个布局。", "导出完成");
                }
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show($"导出过程中发生错误：{ex.Message}", "错误");
            }
            finally
            {
                IsRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 导出单个布局
        /// </summary>
        private void ExportLayout(string layoutName)
        {
            var project = Project.Current;
            var layoutItem = project.GetItems<LayoutProjectItem>().FirstOrDefault(l => l.Name == layoutName);
            
            if (layoutItem == null) return;

            var layout = layoutItem.GetLayout();
            if (layout == null) return;

            string fileName = $"{layoutName}.{GetFileExtension()}";
            string filePath = Path.Combine(OutputFolder, fileName);

            // 解析分辨率
            if (!int.TryParse(Resolution, out int resolution))
                resolution = 300;

            // 根据格式导出
            switch (SelectedFormat.ToUpper())
            {
                case "PDF":
                    ExportToPDF(layout, filePath, resolution);
                    break;
                case "TIF":
                case "TIFF":
                    ExportToTIFF(layout, filePath, resolution, false);
                    break;
                case "GEOTIFF":
                    ExportToGeoTIFF(layout, filePath, resolution);
                    break;
                case "JPG":
                case "JPEG":
                    ExportToJPEG(layout, filePath, resolution);
                    break;
                case "PNG":
                    ExportToPNG(layout, filePath, resolution);
                    break;
            }
        }

        /// <summary>
        /// 导出为PDF
        /// </summary>
        private void ExportToPDF(Layout layout, string filePath, int resolution)
        {
            var exportFormat = new PDFFormat()
            {
                Resolution = resolution,
                OutputFileName = filePath
            };
            layout.Export(exportFormat);
        }

        /// <summary>
        /// 导出为TIFF
        /// </summary>
        private void ExportToTIFF(Layout layout, string filePath, int resolution, bool withWorldFile)
        {
            var exportFormat = new TIFFFormat()
            {
                Resolution = resolution,
                OutputFileName = filePath,
                HasWorldFile = withWorldFile
            };
            layout.Export(exportFormat);
        }

        /// <summary>
        /// 导出为GeoTIFF（带地理参考信息）
        /// </summary>
        private void ExportToGeoTIFF(Layout layout, string filePath, int resolution)
        {
            // 获取布局中的地图框架
            string mapFrameName = GetValidMapFrameName(layout);

            if (string.IsNullOrEmpty(mapFrameName))
            {
                // 如果没有找到有效的地图框架，回退到普通TIFF导出
                PresentationServices.Dialogs.Show(
                    "布局中没有找到有效的地图框架，将导出为普通TIFF格式。\n" +
                    "要导出GeoTIFF，请确保布局中包含至少一个地图框架且该框架有有效的坐标系统。",
                    "地理参考警告");
                ExportToTIFF(layout, filePath, resolution, true);
                return;
            }

            // 创建TIFF导出格式，指定地图框架作为地理参考源
            var exportFormat = new TIFFFormat()
            {
                Resolution = resolution,
                OutputFileName = filePath,
                HasWorldFile = true,                    // 生成世界文件(.tfw)
                HasGeoTiffTags = true,                  // 嵌入GeoTIFF标签到TIFF文件中
                ImageCompressionQuality = 100,          // 最高质量，确保精度
                GeoReferenceMapFrameName = mapFrameName // 指定用于地理参考的地图框架
            };

            layout.Export(exportFormat);
        }

        /// <summary>
        /// 导出为JPEG
        /// </summary>
        private void ExportToJPEG(Layout layout, string filePath, int resolution)
        {
            var exportFormat = new JPEGFormat()
            {
                Resolution = resolution,
                OutputFileName = filePath
            };
            layout.Export(exportFormat);
        }

        /// <summary>
        /// 导出为PNG
        /// </summary>
        private void ExportToPNG(Layout layout, string filePath, int resolution)
        {
            var exportFormat = new PNGFormat()
            {
                Resolution = resolution,
                OutputFileName = filePath
            };
            layout.Export(exportFormat);
        }
    }
}
