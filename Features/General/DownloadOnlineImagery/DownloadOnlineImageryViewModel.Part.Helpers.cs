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
        /// 初始化数据
        /// </summary>
        private void InitializeData()
        {
            // 初始化下载级别
            DownloadLevels = new ObservableCollection<DownloadLevelItem>
            {
                new DownloadLevelItem { Level = "9", Description = "9级", Scale = 1155581 },
                new DownloadLevelItem { Level = "10", Description = "10级", Scale = 577791 },
                new DownloadLevelItem { Level = "11", Description = "11级", Scale = 288895 },
                new DownloadLevelItem { Level = "12", Description = "12级", Scale = 144448 },
                new DownloadLevelItem { Level = "13", Description = "13级", Scale = 72224 },
                new DownloadLevelItem { Level = "14", Description = "14级", Scale = 36112 },
                new DownloadLevelItem { Level = "15", Description = "15级", Scale = 18056 },
                new DownloadLevelItem { Level = "16", Description = "16级", Scale = 9028 },
                new DownloadLevelItem { Level = "17", Description = "17级", Scale = 4514 },
                new DownloadLevelItem { Level = "18", Description = "18级", Scale = 2257 },
                new DownloadLevelItem { Level = "19", Description = "19级", Scale = 1128 },
                new DownloadLevelItem { Level = "20", Description = "20级", Scale = 564 },
                new DownloadLevelItem { Level = "21", Description = "21级", Scale = 282 }
            };

            // 设置默认级别为15级
            SelectedDownloadLevel = DownloadLevels.FirstOrDefault(x => x.Level == "15");

            // 设置默认输出文件夹
            try
            {
                var project = Project.Current;
                if (project != null)
                {
                    OutputFolder = Path.Combine(Path.GetDirectoryName(project.Path), "DownloadedImagery");
                }
                else
                {
                    OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DownloadedImagery");
                }
            }
            catch
            {
                OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DownloadedImagery");
            }

            AddLogMessage("工具已初始化，请选择要素图层和输出文件夹。");
        }

        /// <summary>
        /// 英寸转米
        /// </summary>
        private double InchesToMeters(double inches)
        {
            return inches * 0.0254; // 1 英寸 = 0.0254 米
        }

        /// <summary>
        /// 删除与指定文件相关的所有辅助文件。
        /// </summary>
        private async Task DeleteRelatedFilesAsync(string mainFilePath, bool includeMainFile = false)
        {
            var cleanup = await _fileLifecycle.DeleteRelatedFilesAsync(
                mainFilePath,
                includeMainFile,
                System.Threading.CancellationToken.None);
            foreach (var deleted in cleanup.DeletedFiles)
            {
                AddLogMessage($"已删除{deleted.Category}: {deleted.FileName}");
            }

            foreach (var failed in cleanup.FailedFiles)
            {
                AddLogMessage($"删除{failed.Category} {failed.FileName} 失败: {failed.ErrorMessage}");
            }
        }

        /// <summary>
        /// 隐藏所有可见的临时图层
        /// </summary>
        private void HideAllTemporaryLayers()
        {
            try
            {
                var map = MapView.Active?.Map;
                if (map != null)
                {
                    var tempLayers = map.GetLayersAsFlattenedList()
                        .Where(layer =>
                            (layer.Name.Contains("temp_grid") ||
                             layer.Name.Contains("selected_grid") ||
                             layer.Name.Contains("index_grid") ||
                             layer.Name.Contains("fishnet") ||
                             layer.Name.ToLower().Contains("grid")) &&
                            layer.IsVisible)
                        .ToList();

                    foreach (var layer in tempLayers)
                    {
                        layer.SetVisibility(false);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"隐藏临时图层时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理输出文件夹中的临时文件
        /// </summary>
        private async Task CleanupTemporaryFiles()
        {
            AddLogMessage("正在清理输出文件夹中的临时文件...");
            var cleanup = await _fileLifecycle.DeleteTemporaryFilesAsync(
                OutputFolder,
                System.Threading.CancellationToken.None);
            foreach (var deleted in cleanup.DeletedFiles)
            {
                AddLogMessage($"已删除临时文件: {deleted.FileName}");
            }

            foreach (var failed in cleanup.FailedFiles)
            {
                AddLogMessage($"删除临时文件 {failed.FileName} 失败: {failed.ErrorMessage}");
            }

            AddLogMessage(cleanup.DeletedCount > 0
                ? $"共清理了 {cleanup.DeletedCount} 个临时文件。"
                : "未发现需要清理的临时文件。");
        }

        /// <summary>
        /// 清理所有临时图层
        /// </summary>
        private async Task CleanupTemporaryLayers()
        {
            await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                if (map != null)
                {
                    // 查找并移除所有可能的临时图层
                    var layersToRemove = map.GetLayersAsFlattenedList()
                        .Where(layer =>
                            layer.Name.Contains("temp_grid") ||
                            layer.Name.Contains("selected_grid") ||
                            layer.Name.Contains("index_grid") ||
                            layer.Name.Contains("fishnet") ||
                            layer.Name.ToLower().Contains("grid") &&
                            (layer.Name.Contains("temp") || layer.Name.Contains("临时")))
                        .ToList();

                    PresentationServices.UiThread.Invoke(() =>
                    {
                        if (layersToRemove.Count > 0)
                        {
                            AddLogMessage($"发现 {layersToRemove.Count} 个临时图层，正在清理...");
                        }
                    });

                    foreach (var layer in layersToRemove)
                    {
                        try
                        {
                            map.RemoveLayer(layer);
                            PresentationServices.UiThread.Invoke(() =>
                            {
                                AddLogMessage($"已移除临时图层: {layer.Name}");
                            });
                        }
                        catch (Exception ex)
                        {
                            PresentationServices.UiThread.Invoke(() =>
                            {
                                AddLogMessage($"移除临时图层 {layer.Name} 时出错: {ex.Message}");
                            });
                        }
                    }

                    // 强制刷新地图
                    MapView.Active?.Redraw(true);
                }
            });
        }

        /// <summary>
        /// 为范围添加缓冲区
        /// </summary>
        private Envelope AddBufferToExtent(Envelope extent, double bufferFactor, SpatialReference spatialReference)
        {
            double xBuffer = (extent.XMax - extent.XMin) * bufferFactor;
            double yBuffer = (extent.YMax - extent.YMin) * bufferFactor;

            return EnvelopeBuilderEx.CreateEnvelope(
                extent.XMin - xBuffer,
                extent.YMin - yBuffer,
                extent.XMax + xBuffer,
                extent.YMax + yBuffer,
                spatialReference);
        }

        protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
