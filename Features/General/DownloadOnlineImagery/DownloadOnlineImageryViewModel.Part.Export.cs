using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks; 
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Text;
using System.Globalization;

namespace XIAOFUTools.Features.General.DownloadOnlineImagery
{
    internal partial class DownloadOnlineImageryViewModel
    {

        /// <summary>
        /// 创建格网并导出影像
        /// </summary>
        private async Task CreateGridAndExportImages(Envelope extent, GridParameters gridParams, SpatialReference spatialReference)
        {
            AddLogMessage("开始创建格网...");

            // 创建临时格网
            string tempFolder = Path.GetTempPath();
            string gridOutput = Path.Combine(tempFolder, $"temp_grid_{Guid.NewGuid():N}.shp");

            try
            {
                // 计算格网的行列数
                int numRows = (int)Math.Ceiling((extent.YMax - extent.YMin) / gridParams.CellHeight);
                int numColumns = (int)Math.Ceiling((extent.XMax - extent.XMin) / gridParams.CellWidth);

                // 使用ArcGIS Pro的CreateFishnet工具创建格网（不添加到地图）
                var parameters = Geoprocessing.MakeValueArray(
                    gridOutput,                                    // out_feature_class
                    $"{extent.XMin} {extent.YMin}",               // origin_coord
                    $"{extent.XMin} {extent.YMin + gridParams.CellHeight}", // y_axis_coord
                    gridParams.CellWidth,                         // cell_width
                    gridParams.CellHeight,                        // cell_height
                    numRows,                                      // number_rows
                    numColumns,                                   // number_columns
                    $"{extent.XMax} {extent.YMax}",              // corner_coord
                    "NO_LABELS",                                  // labels
                    extent,                                       // template
                    "POLYGON"                                     // geometry_type
                );

                // 设置环境变量，覆盖输出
                var env = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

                // 设置执行标志，不添加输出到地图
                var executeFlags = GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;

                AddLogMessage("正在创建格网...");
                var result = await Geoprocessing.ExecuteToolAsync("management.CreateFishnet", parameters, env, null, null, executeFlags);
                if (result.IsFailed)
                {
                    throw new Exception($"创建格网失败: {string.Join(", ", result.Messages.Select(m => m.Text))}");
                }

                AddLogMessage("格网创建完成，开始导出影像...");

                // 导出影像
                await ExportImagesFromGrid(gridOutput, spatialReference);

                // 如果需要合并影像
                if (MergeImages)
                {
                    await MergeExportedImages();
                }
            }
            finally
            {
                // 清理临时文件
                try
                {
                    if (_fileLifecycle.FileExists(gridOutput))
                    {
                        AddLogMessage("清理临时格网文件...");
                        var cleanup = await _fileLifecycle.DeleteRelatedFilesAsync(
                            gridOutput,
                            includeMainFile: true,
                            System.Threading.CancellationToken.None);
                        foreach (var deleted in cleanup.DeletedFiles)
                        {
                            AddLogMessage($"已删除格网文件: {deleted.FileName}");
                        }

                        foreach (var failed in cleanup.FailedFiles)
                        {
                            AddLogMessage($"删除格网文件 {failed.FileName} 失败: {failed.ErrorMessage}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLogMessage($"清理格网文件时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 从格网导出影像
        /// </summary>
        private async Task ExportImagesFromGrid(string gridPath, SpatialReference spatialReference)
        {
            var exportedFiles = new List<string>();

            AddLogMessage("正在读取格网并导出影像...");

            // 保存选中要素图层的原始可见性状态
            bool originalSelectedLayerVisibility = false;
            FeatureLayer gridLayer = null;

            await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                var mapView = MapView.Active;

                if (map == null || mapView == null)
                {
                    throw new Exception("无法获取当前地图视图");
                }

                if (SelectedFeatureLayer != null)
                {
                    originalSelectedLayerVisibility = SelectedFeatureLayer.IsVisible;
                    // 临时隐藏选中的要素图层，避免影响导出效果
                    SelectedFeatureLayer.SetVisibility(false);

                    PresentationServices.UiThread.Invoke(() =>
                    {
                        AddLogMessage($"已临时隐藏要素图层: {SelectedFeatureLayer.Name}");
                    });
                }

                // 添加格网图层到地图（立即隐藏）
                gridLayer = LayerFactory.Instance.CreateLayer(new Uri(gridPath), map) as FeatureLayer;

                try
                {
                    if (gridLayer != null)
                    {
                        // 立即隐藏格网图层，避免在地图上显示
                        gridLayer.SetVisibility(false);

                        // 强制刷新地图以确保图层被隐藏
                        MapView.Active?.Redraw(true);

                        PresentationServices.UiThread.Invoke(() =>
                        {
                            AddLogMessage($"已创建并隐藏格网图层: {gridLayer.Name}");
                        });
                        var featureClass = gridLayer.GetFeatureClass();
                        int totalCount = (int)featureClass.GetCount();
                        int currentIndex = 0;

                        PresentationServices.UiThread.Invoke(() =>
                        {
                            AddLogMessage($"找到 {totalCount} 个格网单元，开始导出影像...");
                            if (totalCount > 50)
                            {
                                AddLogMessage($"警告: 格网数量较多({totalCount}个)，导出可能需要较长时间。");
                            }
                        });

                        using (var cursor = featureClass.Search())
                        {
                            while (cursor.MoveNext())
                            {
                                // 检查是否取消
                                if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
                                {
                                    PresentationServices.UiThread.Invoke(() =>
                                    {
                                        AddLogMessage("用户取消了下载操作。");
                                    });
                                    break;
                                }

                                using (var feature = cursor.Current)
                                {
                                    var geometry = feature["Shape"] as Polygon;
                                    if (geometry != null)
                                    {
                                        var gridExtent = geometry.Extent;

                                        // 添加缓冲区
                                        var bufferedExtent = AddBufferToExtent(gridExtent, 0.05, spatialReference);

                                        // 确保所有临时图层都隐藏
                                        HideAllTemporaryLayers();

                                        // 设置地图范围
                                        mapView.ZoomTo(bufferedExtent);

                                        // 等待地图刷新并确保视图更新
                                        System.Threading.Thread.Sleep(500);

                                        // 再次确保所有临时图层隐藏，然后刷新地图视图
                                        HideAllTemporaryLayers();
                                        mapView.Redraw(true);

                                        // 再等待一下确保地图完全刷新
                                        System.Threading.Thread.Sleep(200);

                                        // 导出影像
                                        string fileName = $"image_{currentIndex:D4}_{gridExtent.XMin:F0}_{gridExtent.YMin:F0}.tif";
                                        string outputPath = Path.Combine(OutputFolder, fileName);

                                        try
                                        {
                                            // 创建TIFF导出格式
                                            var tiffFormat = new TIFFFormat()
                                            {
                                                OutputFileName = outputPath,
                                                Resolution = 100,
                                                Height = 1080,
                                                Width = 1660,
                                                ColorMode = TIFFColorMode.TwentyFourBitTrueColor,
                                                HasWorldFile = true,
                                                ImageCompression = TIFFImageCompression.LZW
                                            };

                                            // 验证输出路径
                                            if (tiffFormat.ValidateOutputFilePath())
                                            {
                                                // 导出地图视图
                                                mapView.Export(tiffFormat);

                                                // 验证文件是否成功创建
                                                if (_fileLifecycle.FileExists(outputPath))
                                                {
                                                    exportedFiles.Add(outputPath);
                                                }
                                                else
                                                {
                                                    PresentationServices.UiThread.Invoke(() =>
                                                    {
                                                        AddLogMessage($"警告: 影像文件 {fileName} 未成功创建");
                                                    });
                                                }
                                            }
                                            else
                                            {
                                                PresentationServices.UiThread.Invoke(() =>
                                                {
                                                    AddLogMessage($"错误: 输出路径验证失败 {outputPath}");
                                                });
                                            }
                                        }
                                        catch (Exception exportEx)
                                        {
                                            PresentationServices.UiThread.Invoke(() =>
                                            {
                                                AddLogMessage($"导出影像 {fileName} 时出错: {exportEx.Message}");
                                            });
                                        }

                                        currentIndex++;

                                        // 更新进度 (20%-95%之间)
                                        PresentationServices.UiThread.Invoke(() =>
                                        {
                                            // 导出进度占总进度的75% (从20%到95%)
                                            int exportProgress = 20 + (int)((double)currentIndex / totalCount * 75);
                                            Progress = exportProgress;

                                            if (currentIndex % 5 == 0 || currentIndex == totalCount)
                                            {
                                                AddLogMessage($"已导出 {currentIndex}/{totalCount} 个影像 ({exportProgress}%)");
                                            }
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.Invoke(() =>
                    {
                        AddLogMessage($"导出影像时出错: {ex.Message}");
                    });
                    throw;
                }
            });

            // 清理工作：移除临时图层并恢复可见性
            await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                if (map != null)
                {
                    // 移除临时格网图层
                    if (gridLayer != null)
                    {
                        map.RemoveLayer(gridLayer);
                        PresentationServices.UiThread.Invoke(() =>
                        {
                            AddLogMessage($"已移除格网图层: {gridLayer.Name}");
                        });
                    }

                    // 恢复选中要素图层的原始可见性状态
                    if (SelectedFeatureLayer != null)
                    {
                        SelectedFeatureLayer.SetVisibility(originalSelectedLayerVisibility);
                        PresentationServices.UiThread.Invoke(() =>
                        {
                            AddLogMessage($"已恢复要素图层可见性: {SelectedFeatureLayer.Name} -> {originalSelectedLayerVisibility}");
                        });
                    }
                }
            });

            // 保存导出的文件列表
            _exportedImageFiles = exportedFiles;
            AddLogMessage($"影像导出完成，共 {exportedFiles.Count} 个文件。");
        }

        /// <summary>
        /// 合并导出的影像
        /// </summary>
        private async Task MergeExportedImages()
        {
            if (_exportedImageFiles.Count == 0)
            {
                AddLogMessage("没有影像文件需要合并。");
                return;
            }

            // 合并进度 (95%-98%)
            Progress = 95;
            AddLogMessage("开始合并影像...");

            try
            {
                string mergedImagePath = Path.Combine(OutputFolder, "merged_output.tif");

                var parameters = Geoprocessing.MakeValueArray(
                    string.Join(";", _exportedImageFiles),
                    OutputFolder,
                    "merged_output.tif",
                    "",
                    "8_BIT_UNSIGNED",
                    "",
                    3,
                    "BLEND",
                    "FIRST"
                );
                var result = await Geoprocessing.ExecuteToolAsync(
                    "management.MosaicToNewRaster",
                    parameters,
                    null,
                    null,
                    null,
                    GPExecuteToolFlags.GPThread);
                if (result.IsFailed)
                {
                    throw new Exception($"合并影像失败: {string.Join(", ", result.Messages.Select(message => message.Text))}");
                }

                Progress = 96;
                AddLogMessage($"影像合并完成: {mergedImagePath}");

                // 构建金字塔
                Progress = 97;
                await BuildPyramids(mergedImagePath);

                // 删除临时影像文件及其辅助文件
                Progress = 98;
                if (_fileLifecycle.FileExists(mergedImagePath))
                {
                    AddLogMessage("删除临时影像文件及辅助文件...");
                    foreach (var file in _exportedImageFiles)
                    {
                        try
                        {
                            await DeleteRelatedFilesAsync(file, includeMainFile: true);
                        }
                        catch (Exception ex)
                        {
                            AddLogMessage($"删除文件 {file} 失败: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"合并影像时出错: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 构建金字塔
        /// </summary>
        private async Task BuildPyramids(string imagePath)
        {
            try
            {
                AddLogMessage("正在构建金字塔...");

                var parameters = Geoprocessing.MakeValueArray(imagePath);
                var result = await Geoprocessing.ExecuteToolAsync(
                    "management.BuildPyramids",
                    parameters,
                    null,
                    null,
                    null,
                    GPExecuteToolFlags.GPThread);

                if (result.IsFailed)
                {
                    AddLogMessage($"构建金字塔警告: {string.Join(", ", result.Messages.Select(message => message.Text))}");
                }

                AddLogMessage("金字塔构建完成。");
            }
            catch (Exception ex)
            {
                AddLogMessage($"构建金字塔时出错: {ex.Message}");
            }
        }
    }
}
