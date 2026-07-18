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
        /// 加载要素图层
        /// </summary>
        private async void LoadFeatureLayers()
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map != null)
                    {
                        var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                        
                        PresentationServices.UiThread.Invoke(() =>
                        {
                            FeatureLayers = new ObservableCollection<Layer>(layers);
                            if (FeatureLayers.Count > 0)
                            {
                                SelectedFeatureLayer = FeatureLayers.First();
                                AddLogMessage($"已加载 {FeatureLayers.Count} 个要素图层。");
                            }
                            else
                            {
                                AddLogMessage("当前地图中没有要素图层。");
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                AddLogMessage($"加载要素图层时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 是否可以开始下载
        /// </summary>
        private bool CanStartDownload()
        {
            return !IsProcessing &&
                   SelectedFeatureLayer != null &&
                   !string.IsNullOrEmpty(OutputFolder) &&
                   SelectedDownloadLevel != null;
        }

        /// <summary>
        /// 是否可以停止下载
        /// </summary>
        private bool CanStopDownload()
        {
            return IsProcessing;
        }

        /// <summary>
        /// 停止下载
        /// </summary>
        private void StopDownload()
        {
            try
            {
                if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                    AddLogMessage("正在停止下载操作...");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"停止下载时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始下载
        /// </summary>
        private async void StartDownload()
        {
            if (!CanStartDownload())
            {
                AddLogMessage("下载条件不满足，请检查参数设置。");
                return;
            }

            IsProcessing = true;
            IsProgressIndeterminate = false;  // 改为确定进度模式
            Progress = 0;

            // 创建取消令牌
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new System.Threading.CancellationTokenSource();

            try
            {
                AddLogMessage("开始下载在线影像...");
                AddLogMessage($"要素图层: {SelectedFeatureLayer.Name}");
                AddLogMessage($"输出文件夹: {OutputFolder}");
                AddLogMessage($"下载级别: {SelectedDownloadLevel}");
                AddLogMessage($"合并影像: {(MergeImages ? "是" : "否")}");

                // 首先清理任何现有的临时图层
                await CleanupTemporaryLayers();

                // 等待一下确保清理完成
                await Task.Delay(500);

                await Task.Run(() => PerformDownload());
            }
            catch (Exception ex)
            {
                AddLogMessage($"下载过程中出错: {ex.Message}");
            }
            finally
            {
                // 确保清理所有临时图层
                try
                {
                    await CleanupTemporaryLayers();
                }
                catch (Exception ex)
                {
                    AddLogMessage($"清理临时图层时出错: {ex.Message}");
                }

                // 清理输出文件夹中的临时文件
                try
                {
                    await CleanupTemporaryFiles();
                }
                catch (Exception ex)
                {
                    AddLogMessage($"清理临时文件时出错: {ex.Message}");
                }

                IsProcessing = false;
                IsProgressIndeterminate = false;
                Progress = 100;
            }
        }

        /// <summary>
        /// 执行下载
        /// </summary>
        private async Task PerformDownload()
        {
            try
            {
                // 步骤1: 准备工作 (5%)
                Progress = 5;
                AddLogMessage("正在准备下载...");

                // 确保输出文件夹存在
                if (await _fileLifecycle.EnsureDirectoryAsync(
                        OutputFolder,
                        _cancellationTokenSource.Token))
                {
                    AddLogMessage($"已创建输出文件夹: {OutputFolder}");
                }

                // 步骤2: 获取要素图层范围 (10%)
                Progress = 10;
                AddLogMessage("正在获取要素图层范围...");

                Envelope extent = null;
                SpatialReference spatialReference = null;

                await QueuedTask.Run(() =>
                {
                    if (SelectedFeatureLayer is FeatureLayer featureLayer)
                    {
                        extent = featureLayer.QueryExtent();
                        spatialReference = featureLayer.GetSpatialReference();
                    }
                });

                if (extent == null)
                {
                    AddLogMessage("无法获取要素图层范围。");
                    return;
                }

                // 步骤3: 计算格网参数 (15%)
                Progress = 15;
                AddLogMessage($"要素范围: X({extent.XMin:F2}, {extent.XMax:F2}), Y({extent.YMin:F2}, {extent.YMax:F2})");
                AddLogMessage($"坐标系: {spatialReference?.Name ?? "未知"}");

                var scale = SelectedDownloadLevel.Scale;
                var gridParams = CalculateGridParameters(extent, scale, spatialReference);

                AddLogMessage($"格网大小: {gridParams.CellWidth:F6} x {gridParams.CellHeight:F6}");
                AddLogMessage($"格网数量: {gridParams.GridCount} 个");

                // 步骤4: 创建格网并导出影像 (20%-95%)
                Progress = 20;

                // 检查是否取消
                if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    AddLogMessage("操作已取消。");
                    return;
                }

                await CreateGridAndExportImages(extent, gridParams, spatialReference);

                // 步骤5: 完成 (100%)
                Progress = 100;
                AddLogMessage("影像下载完成。");
            }
            catch (Exception ex)
            {
                AddLogMessage($"下载过程出错: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 计算格网参数
        /// </summary>
        private GridParameters CalculateGridParameters(Envelope extent, int scale, SpatialReference spatialReference)
        {
            // 格网尺寸（英寸）
            double gridWidthInch = 12.42425;
            double gridHeightInch = 7.42425;

            double cellWidth, cellHeight;

            if (spatialReference.IsGeographic)
            {
                // 地理坐标系
                double metersPerDegree = 111000; // 粗略估计
                double widthMeters = InchesToMeters(gridWidthInch) * scale;
                double heightMeters = InchesToMeters(gridHeightInch) * scale;
                cellWidth = widthMeters / metersPerDegree;
                cellHeight = heightMeters / metersPerDegree;
            }
            else
            {
                // 投影坐标系
                cellWidth = InchesToMeters(gridWidthInch) * scale;
                cellHeight = InchesToMeters(gridHeightInch) * scale;
            }

            // 计算格网数量
            int xCount = (int)Math.Ceiling((extent.XMax - extent.XMin) / cellWidth);
            int yCount = (int)Math.Ceiling((extent.YMax - extent.YMin) / cellHeight);
            int gridCount = xCount * yCount;

            return new GridParameters
            {
                CellWidth = cellWidth,
                CellHeight = cellHeight,
                GridCount = gridCount
            };
        }
    }
}
