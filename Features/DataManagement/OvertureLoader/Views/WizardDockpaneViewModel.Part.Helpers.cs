using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Geometry;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Data;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Services;
using Microsoft.Win32;
using ArcGIS.Desktop.Catalog;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.IO;
using System.Threading;
using ArcGIS.Desktop.Core; // Added for Project.Current
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Views
{
    internal partial class WizardDockpaneViewModel
    {

        protected override async Task InitializeAsync()
        {
            try
            {
                AddToLog("异步初始化: 正在初始化 DuckDB");
                await _dataProcessor.InitializeDuckDBAsync();
                AddToLog("异步初始化: 正在获取最新版本信息");
                LatestRelease = await GetLatestRelease();
                NotifyPropertyChanged(nameof(LatestRelease));
                AddToLog($"异步初始化: 最新版本设置为: {LatestRelease}");

                InitializeThemes(); // Populate Themes collection
                AddToLog("异步初始化: 主题已初始化");

                var defaultBasePath = DeterminedDefaultMfcBasePath;
                DataOutputPath = Path.Combine(defaultBasePath, "Data", LatestRelease ?? "latest");
                NotifyPropertyChanged(nameof(DataOutputPath));
                AddToLog($"异步初始化: 数据输出路径更新为: {DataOutputPath}");

                StatusText = "准备加载 Overture Maps 数据";
                AddToLog("异步初始化: 准备加载 Overture Maps 数据");
            }
            catch (Exception ex)
            {
                var error = $"Async Initialization error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error during async initialization: {ex}");
                StatusText = error;
                AddToLog($"ERROR: {error}");
            }
            finally
            {
                IsLoading = false;
                NotifyPropertyChanged(nameof(IsLoading));
            }
        }

        private void SetCustomExtent()
        {
            try
            {
                // Add diagnostic logging
                System.Diagnostics.Debug.WriteLine("SetCustomExtent method called");
                AddToLog("调用SetCustomExtent方法 - 尝试激活绘图工具");

                // Ensure the custom extent radio button is selected
                UseCustomExtent = true;

                // Make sure we're subscribed to the static event
                // We already subscribed in the constructor, but ensure it's still active
                try
                {
                    // Remove any existing subscription and add it again to be safe
                    // This prevents multiple handlers if called multiple times
                    CustomExtentTool.ExtentCreatedStatic -= OnExtentCreated;
                    CustomExtentTool.ExtentCreatedStatic += OnExtentCreated;
                    System.Diagnostics.Debug.WriteLine("Re-established event subscription for CustomExtentTool");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error managing event subscriptions: {ex.Message}");
                }

                // Create the instance tool as well for backward compatibility
                if (_customExtentTool == null)
                {
                    _customExtentTool = new CustomExtentTool();
                    _customExtentTool.ExtentCreated += OnExtentCreated;
                }

                // Use ArcGIS Pro's drawing tool to select an extent
                QueuedTask.Run(async () =>
                {
                    System.Diagnostics.Debug.WriteLine("Inside QueuedTask.Run");
                    AddToLog("开始自定义范围绘制操作...");

                    // Get a reference to the active map and make sure one exists
                    var mapView = MapView.Active;
                    if (mapView == null)
                    {
                        AddToLog("无法设置自定义范围: 没有活动的地图视图");
                        PresentationServices.Dialogs.Show(
                            "请在设置自定义范围之前打开地图。",
                            "无活动地图");
                        return;
                    }

                    AddToLog($"找到活动地图视图: {mapView.Map.Name}");

                    try
                    {
                        // Activate our custom tool
                        AddToLog("激活自定义绘图工具...");
                        System.Diagnostics.Debug.WriteLine("Activating custom extent tool");

                        // Use our custom tool by ID as defined in the Config.daml
                        await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_CustomExtentTool");
                        AddToLog("在地图上绘制矩形以设置自定义范围...");
                        System.Diagnostics.Debug.WriteLine("Custom tool activated successfully");
                    }
                    catch (Exception ex)
                    {
                        AddToLog($"工具激活错误: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Exception in tool activation: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                AddToLog($"设置自定义范围错误: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception in SetCustomExtent: {ex}");
            }
        }

        /// <summary>
        /// Performs bulk folder deletion and layer removal asynchronously to prevent UI blocking
        /// </summary>
        private async Task PerformBulkDataReplacementAsync(List<SelectableThemeItem> selectedItems, string dataPath)
        {
            var cancellationToken = _cts?.Token ?? CancellationToken.None;
            try
            {
                var progress = new Progress<OvertureCleanupProgress>(update =>
                {
                    var folderName = Path.GetFileName(update.FolderPath);
                    if (update.Stage == OvertureCleanupStage.RemovingMapMembers)
                    {
                        StatusText = $"正在移除 {folderName} 图层 ({update.Current}/{update.Total})...";
                        AddToLog($"正在移除使用 {folderName} 数据的图层和独立表");
                    }
                    else
                    {
                        StatusText = $"正在删除 {folderName} 目录 ({update.Current}/{update.Total})...";
                        AddToLog($"正在删除数据目录: {folderName}");
                    }
                });

                var result = await _dataReplacementService.CleanupAsync(
                    dataPath,
                    selectedItems.Select(item => item.ActualType),
                    progress,
                    cancellationToken);

                AddToLog(
                    $"数据清理完成：目录 {result.ExistingFolderCount} 个，" +
                    $"移除地图成员 {result.RemovedMapMemberCount} 个，删除目录 {result.DeletedFolderCount} 个");
                foreach (var issue in result.Issues)
                {
                    AddToLog($"清理警告 [{Path.GetFileName(issue.FolderPath)}]: {issue.Message}");
                }
                StatusText = result.Issues.Count == 0
                    ? "现有数据清理完成，准备加载新数据..."
                    : "现有数据清理完成，但存在警告";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                StatusText = "数据清理已取消";
                AddToLog("用户取消了现有数据清理");
            }
            catch (Exception ex)
            {
                AddToLog($"清理现有数据时发生警告: {ex.Message}");
                StatusText = "数据清理存在警告，将继续加载";
                System.Diagnostics.Debug.WriteLine($"清理 Overture 现有数据失败: {ex}");
            }
        }

        // Cleanup method that will be called when the add-in is unloaded
        // No override needed - this will be called by the framework
        private void CleanupResources()
        {
            // Cleanup by unsubscribing from static events
            System.Diagnostics.Debug.WriteLine("WizardDockpaneViewModel cleaning up, unsubscribing from static events");
            CustomExtentTool.ExtentCreatedStatic -= OnExtentCreated;

            // Dispose of the cancellation token source if it exists
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
        }

        ~WizardDockpaneViewModel()
        {
            CleanupResources();
        }

    }
}
