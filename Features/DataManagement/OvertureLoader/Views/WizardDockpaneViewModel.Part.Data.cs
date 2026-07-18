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
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;
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

        private async Task<string> GetLatestRelease()
        {
            try
            {
                _releaseClient ??= OvertureReleaseClient.CreateDefault();
                var release = await _releaseClient.GetLatestReleaseAsync(CancellationToken.None);
                AddToLog("已从 Overture Maps API 接收版本信息");
                AddToLog($"可用的最新版本: {release}");
                return release;
            }
            catch (Exception ex)
            {
                AddToLog($"获取最新版本失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error getting latest release: {ex}");
                throw;
            }
        }

        private async Task LoadOvertureDataAsync()
        {
            try
            {
                var selectedLeafItems = GetSelectedLeafItems();
                if (selectedLeafItems.Count == 0)
                {
                    AddToLog("未选择任何主题或子主题。");
                    return;
                }

                // Initialize a new cancellation token source
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                var cancellationToken = _cts.Token;

                // Switch to status tab
                SelectedTabIndex = 1;

                StatusText = $"正在加载 {selectedLeafItems.Count} 个选定的数据类型...";
                AddToLog($"开始从版本 {LatestRelease} 加载 {selectedLeafItems.Count} 个数据类型");
                AddToLog($"预计处理时间: {EstimateProcessingTime(selectedLeafItems.Count)} 分钟");

                var extentResolution = await QueuedTask.Run(() =>
                {
                    var messages = new List<string>();
                    Envelope resolvedExtent = null;
                    SpatialReference wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326);

                    if (UseCurrentMapExtent && MapView.Active != null)
                    {
                        Envelope mapExtent = MapView.Active.Extent;
                        if (mapExtent != null)
                        {
                            if (mapExtent.SpatialReference == null || mapExtent.SpatialReference.Wkid != 4326)
                            {
                                messages.Add($"Map extent SR is {mapExtent.SpatialReference?.Wkid.ToString() ?? "null"}, projecting to WGS84 (4326).");
                                try
                                {
                                    resolvedExtent = GeometryEngine.Instance.Project(mapExtent, wgs84) as Envelope;
                                    if (resolvedExtent == null)
                                    {
                                        messages.Add("Warning: Map extent projection to WGS84 resulted in null. Original extent will be used.");
                                        resolvedExtent = mapExtent;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    messages.Add($"Error projecting map extent: {ex.Message}. Using original extent values.");
                                    resolvedExtent = mapExtent;
                                }
                            }
                            else
                            {
                                messages.Add("Map extent is already in WGS84.");
                                resolvedExtent = mapExtent;
                            }
                        }
                        else
                        {
                            messages.Add("Map extent is null.");
                        }
                    }
                    else if (UseCustomExtent && CustomExtent != null)
                    {
                        resolvedExtent = CustomExtent;
                    }
                    else
                    {
                        messages.Add("No extent specified or available for filtering.");
                    }

                    OvertureExtent? pureExtent = resolvedExtent == null
                        ? null
                        : new OvertureExtent(
                            resolvedExtent.XMin,
                            resolvedExtent.YMin,
                            resolvedExtent.XMax,
                            resolvedExtent.YMax);
                    if (pureExtent != null)
                    {
                        messages.Add($"Using WGS84 extent: {pureExtent.XMin:F6}, {pureExtent.YMin:F6}, {pureExtent.XMax:F6}, {pureExtent.YMax:F6}");
                    }
                    return new { Extent = pureExtent, Messages = messages };
                });
                var extent = extentResolution.Extent;
                foreach (var message in extentResolution.Messages)
                {
                    AddToLog(message);
                }

                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    StatusText = "Operation cancelled";
                    AddToLog("Operation was cancelled");
                    return;
                }

                string dataPath = DataOutputPath;
                bool existingDataFound = await _dataReplacementService.HasExistingDataAsync(
                    dataPath,
                    selectedLeafItems.Select(item => item.ActualType),
                    cancellationToken);

                // If existing data is found, confirm with user before overwriting
                if (existingDataFound)
                {
                    var confirmResult = PresentationServices.Dialogs.Show(
                        "发现一个或多个选定主题的现有数据。加载新数据将替换现有文件。\n\n您要继续吗？",
                        "替换现有数据？",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (confirmResult == System.Windows.MessageBoxResult.No)
                    {
                        StatusText = "Operation cancelled by user";
                        AddToLog("Operation cancelled - user chose not to replace existing data");
                        return;
                    }

                    AddToLog("User confirmed replacing existing data");

                    // Perform bulk folder deletion with progress feedback to prevent UI blocking
                    StatusText = "Preparing to replace existing data...";
                    AddToLog("Removing existing layers and deleting theme folders...");
                    await PerformBulkDataReplacementAsync(selectedLeafItems, dataPath);
                    AddToLog("Existing data cleanup completed. Beginning new data loading...");
                }

                int totalDataTypesToProcess = selectedLeafItems.Count;
                int processedDataTypes = 0;

                // Process each selected leaf item (sub-theme or leaf parent theme)
                for (int itemIndex = 0; itemIndex < selectedLeafItems.Count; itemIndex++)
                {
                    var itemToLoad = selectedLeafItems[itemIndex];

                    // Check for cancellation between items
                    if (cancellationToken.IsCancellationRequested)
                    {
                        StatusText = "操作已取消";
                        AddToLog("操作已取消");
                        return;
                    }

                    string parentS3Theme = itemToLoad.ParentThemeForS3;
                    string actualS3Type = itemToLoad.ActualType;
                    string itemDisplayName = itemToLoad.DisplayName; // For logging and layer naming

                    // Enhanced status and logging with better visibility
                    StatusText = $"Processing {itemIndex + 1} of {totalDataTypesToProcess}: {MakeFriendlyName(parentS3Theme)} / {itemDisplayName}";
                    AddToLog($"Processing: {MakeFriendlyName(parentS3Theme)} / {itemDisplayName}");
                    AddToLog($"Data type for S3: theme='{parentS3Theme}', type='{actualS3Type}'");
                    System.Diagnostics.Debug.WriteLine($"Data type for S3: theme='{parentS3Theme}', type='{actualS3Type}'");

                    string trimmedRelease = LatestRelease?.Trim() ?? "";
                    string s3Path = trimmedRelease.Length > 0
                ? $"{S3_BASE_PATH}/{trimmedRelease}/theme={parentS3Theme}/type={actualS3Type}/*.parquet"
                : $"{S3_BASE_PATH}/theme={parentS3Theme}/type={actualS3Type}/*.parquet";

                    AddToLog($"从 S3 路径加载: {s3Path}");
                    System.Diagnostics.Debug.WriteLine($"Loading from S3 path: {s3Path}");

                    // Add explicit UI yield before heavy operations
                    await Task.Delay(50); // Allow UI to update

                    // Create a detailed progress reporter for the S3 data loading phase with heartbeat
                    var ingestProgressReporter = new Progress<string>(status =>
                    {
                        StatusText = status;
                        AddToLog(status);
                        // Update progress to show activity within current item
                        var baseProgress = (processedDataTypes * 100.0) / totalDataTypesToProcess;
                        var ingestProgress = 1.5; // Small increment for S3 loading
                        ProgressValue = Math.Min(baseProgress + ingestProgress, 98.0);
                    });

                    // Add heartbeat timer for long-running S3 operations
                    using var heartbeatCts = new CancellationTokenSource();
                    var heartbeatTask = StartHeartbeatAsync(itemDisplayName, heartbeatCts.Token);

                    StatusText = $"正在从 S3 加载 {itemDisplayName} (可能需要 30-60 秒)...";
                    AddToLog($"⏳ 开始为 {itemDisplayName} 加载 S3 数据 - 请稍候，此操作可能需要一些时间...");

                    bool ingestSuccess = await _dataProcessor.IngestFileAsync(
                        s3Path,
                        extent,
                        actualS3Type,
                        ingestProgressReporter,
                        cancellationToken);

                    // Stop heartbeat
                    heartbeatCts.Cancel();
                    try { await heartbeatTask; } catch (OperationCanceledException) { /* Expected */ }

                    if (!ingestSuccess)
                    {
                        AddToLog($"❌ 从 {s3Path} 加载数据失败");
                        StatusText = $"从 {s3Path} 加载数据时出错";
                        continue; // Skip to next item
                    }

                    AddToLog($"✅ 成功从 S3 加载 {itemDisplayName} 数据");

                    if (cancellationToken.IsCancellationRequested)
                    {
                        StatusText = "Operation cancelled";
                        AddToLog("Operation was cancelled");
                        return;
                    }

                    // Add UI yield before layer creation
                    await Task.Delay(50);

                    // Create a feature layer for the loaded data
                    string featureLayerName = $"{MakeFriendlyName(parentS3Theme)} - {itemDisplayName}";
                    StatusText = $"Creating layers for {itemDisplayName}...";
                    AddToLog($"🔄 Creating feature layers for {itemDisplayName}...");

                    var itemProgressReporter = new Progress<string>(status =>
                    {
                        StatusText = status;
                        AddToLog(status);
                        // Update progress bar with more granular feedback during processing
                        var baseProgress = (processedDataTypes * 100.0) / totalDataTypesToProcess;
                        var itemProgress = 3.0; // Small increment for within-item progress
                        ProgressValue = Math.Min(baseProgress + itemProgress, 99.0); // Don't hit 100 until truly done
                    });

                    await _dataProcessor.CreateFeatureLayerAsync(
                        featureLayerName,
                        itemProgressReporter,
                        parentS3Theme,
                        actualS3Type,
                        DataOutputPath,
                        cancellationToken);

                    processedDataTypes++;
                    ProgressValue = (processedDataTypes * 100.0) / totalDataTypesToProcess;

                    StatusText = $"✅ Completed {itemDisplayName} ({processedDataTypes}/{totalDataTypesToProcess})";
                    AddToLog($"✅ Successfully loaded {itemDisplayName} for {MakeFriendlyName(parentS3Theme)}");

                    // Add small delay between items to ensure UI responsiveness
                    await Task.Delay(100);
                }

                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    StatusText = "Operation cancelled";
                    AddToLog("Operation was cancelled");
                    return;
                }

                // Now that all data is processed, add all layers to map in optimal stacking order
                StatusText = "Adding layers to map in optimal stacking order...";
                AddToLog("🗺️ All data exported successfully. Now adding layers to map with optimal stacking order...");

                // Add UI yield before final layer creation
                await Task.Delay(100);

                var progressReporter = new Progress<string>(status =>
                {
                    StatusText = status;
                    AddToLog(status);
                });

                await _dataProcessor.AddAllLayersToMapAsync(progressReporter);

                // Store the data path for potential MFC creation later
                _lastLoadedDataPath = DataOutputPath;

                // Now that the data is loaded, inform the user they can create an MFC if desired
                AddToLog("----------------");
                AddToLog("数据加载完成。您现在可以:");
                AddToLog("1. 直接使用加载的GeoParquet数据");
                AddToLog("2. 从'创建MFC'选项卡创建多文件要素连接(MFC)");
                AddToLog("注意: 图层已按最佳绘制顺序添加(点在顶部 → 线 → 面在底部)");
                AddToLog("----------------");

                // Show a message box offering to go to the Create MFC tab
                var result = PresentationServices.Dialogs.Show(
                    "数据加载完成。您现在要创建多文件要素连接(MFC)吗？",
                    "创建MFC？",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // Navigate to the Create MFC tab
                    ShowCreateMfcTab();
                }

                // Update the Create MFC command can-execute state
                (CreateMfcCommand as RelayCommand)?.RaiseCanExecuteChanged();

                StatusText = $"Successfully loaded all selected themes from release {LatestRelease}";
                AddToLog($"所有选定主题加载成功");
                AddToLog("----------------");
                if (extent != null)
                {
                    AddToLog($"范围数据: {extent.XMin:F2}, {extent.YMin:F2}, {extent.XMax:F2}, {extent.YMax:F2}");
                    AddToLog("当您为不同范围加载数据时，现有数据将被替换。");
                    AddToLog("这确保了MFC创建的文件夹结构清洁。");
                    AddToLog("如果不想覆盖，请重命名输出文件夹OvertureProAddinData");
                }
                AddToLog("----------------");
                ProgressValue = 100;
            }
            catch (Exception ex)
            {
                // Determine if this is a file access issue
                bool isFileAccessError = ex.Message.Contains("because it is being used by another process") ||
                                         ex.Message.Contains("access") ||
                                         ex.Message.Contains("denied") ||
                                         ex.Message.Contains("locked");

                bool isNetworkError = ex.Message.Contains("network") ||
                                     ex.Message.Contains("timeout") ||
                                     ex.Message.Contains("connection") ||
                                     ex.Message.Contains("DNS") ||
                                     ex.Message.Contains("SSL");

                if (isFileAccessError)
                {
                    StatusText = "文件访问错误";
                    AddToLog($"错误: 文件访问错误。一个或多个文件被其他进程锁定。");
                    AddToLog($"请尝试以下解决方案:");
                    AddToLog($"1. 关闭可能正在使用该数据的其他ArcGIS Pro项目");
                    AddToLog($"2. 从当前地图中移除使用Overture数据的图层");
                    AddToLog($"3. 在极端情况下，重启ArcGIS Pro后重试");
                    AddToLog($"详细错误: {ex.Message}");
                }
                else if (isNetworkError)
                {
                    StatusText = "网络连接错误";
                    AddToLog($"错误: 网络连接问题。");
                    AddToLog($"请检查以下项目:");
                    AddToLog($"1. 确保网络连接正常");
                    AddToLog($"2. 检查防火墙设置是否阻止了AWS S3访问");
                    AddToLog($"3. 如果在企业网络中，请联系网络管理员");
                    AddToLog($"4. 稍后重试，服务器可能暂时不可用");
                    AddToLog($"技术详情: {ex.Message}");
                }
                else
                {
                    StatusText = $"数据加载错误: {ex.Message}";
                    AddToLog($"错误: {ex.Message}");
                    AddToLog($"堆栈跟踪: {ex.StackTrace}");
                }

                ProgressValue = 0;
                System.Diagnostics.Debug.WriteLine($"Load error: {ex}");
            }
            finally
            {
                // Clean up the cancellation token source
                if (_cts != null)
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }
        }
    }
}
