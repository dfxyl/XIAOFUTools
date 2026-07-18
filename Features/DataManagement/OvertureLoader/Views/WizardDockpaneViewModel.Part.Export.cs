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

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new WizardDockpaneView();
        }

        // Handler for when our custom tool creates an extent
        private void OnExtentCreated(Envelope extent)
        {
            // Add more detailed logging
            System.Diagnostics.Debug.WriteLine($"Custom extent created: {extent.XMin}, {extent.YMin}, {extent.XMax}, {extent.YMax}");

            Envelope extentInWGS84 = extent;
            if (extent.SpatialReference == null || extent.SpatialReference.Wkid != 4326)
            {
                AddToLog("Custom extent is not in WGS84. Projecting...");
                System.Diagnostics.Debug.WriteLine($"Original SR WKID: {extent.SpatialReference?.Wkid}");
                SpatialReference wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326);
                try
                {
                    extentInWGS84 = GeometryEngine.Instance.Project(extent, wgs84) as Envelope;
                    if (extentInWGS84 != null)
                    {
                        AddToLog($"Successfully projected custom extent to WGS84: {extentInWGS84.XMin:F6}, {extentInWGS84.YMin:F6}, {extentInWGS84.XMax:F6}, {extentInWGS84.YMax:F6}");
                        System.Diagnostics.Debug.WriteLine($"Projected extent: {extentInWGS84.XMin}, {extentInWGS84.YMin}, {extentInWGS84.XMax}, {extentInWGS84.YMax}");
                    }
                    else
                    {
                        AddToLog("ERROR: Projection to WGS84 resulted in a null envelope. Using original extent.");
                        System.Diagnostics.Debug.WriteLine("ERROR: Projection to WGS84 resulted in a null envelope.");
                        extentInWGS84 = extent; // Fallback to original if projection fails
                    }
                }
                catch (Exception ex)
                {
                    AddToLog($"ERROR: Failed to project custom extent to WGS84: {ex.Message}. Using original extent.");
                    System.Diagnostics.Debug.WriteLine($"ERROR projecting extent: {ex.Message}");
                    extentInWGS84 = extent; // Fallback to original on error
                }
            }
            else
            {
                AddToLog("Custom extent is already in WGS84 or has no spatial reference, assuming WGS84.");
                System.Diagnostics.Debug.WriteLine("Custom extent is already WGS84 or no SR defined.");
            }

            // Store the extent (potentially projected) - this will trigger the property change handlers
            CustomExtent = extentInWGS84;

            // Explicitly set these properties to ensure UI updates
            HasCustomExtent = true;
            UpdateCustomExtentDisplay();
            NotifyPropertyChanged(nameof(CustomExtentDisplay));
            NotifyPropertyChanged(nameof(HasCustomExtent));

            // Make sure custom extent radio is selected
            UseCustomExtent = true; // This will also set UseCurrentMapExtent = false via its setter

            // Ensure tool is deactivated and provide feedback
            QueuedTask.Run(async () => {
                try
                {
                    var mapView = MapView.Active;
                    if (mapView != null)
                    {
                        mapView.CancelDrawing();
                        System.Diagnostics.Debug.WriteLine("MapView.CancelDrawing() called.");
                    }

                    // Deactivate the custom drawing tool and return to the default explore tool
                    // First, explicitly deactivate the current tool (which should be our CustomExtentTool)
                    await FrameworkApplication.SetCurrentToolAsync(null);
                    System.Diagnostics.Debug.WriteLine("Current tool explicitly deactivated (set to null).");

                    // Then, activate the default explore tool
                    await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
                    System.Diagnostics.Debug.WriteLine("Default explore tool activated.");

                    // Give the UI thread a moment for the cursor to update etc.
                    await Task.Delay(300); // Increased delay slightly just in case

                    // Now that the tool is reset, log the next steps and show confirmation
                    AddToLog("自定义范围设置成功，绘图工具已停用。");
                    AddToLog("自定义范围将用于数据加载。"); // User's referenced log
                    AddToLog("您现在可以选择数据主题并点击'加载数据'。");

                    PresentationServices.Dialogs.Show(
                        $"自定义范围设置成功：\n最小 X,Y: {extent.XMin:F4}, {extent.YMin:F4}\n最大 X,Y: {extent.XMax:F4}, {extent.YMax:F4}",
                        "自定义范围已设置",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during tool deactivation or showing custom extent feedback: {ex.Message}");
                    AddToLog($"Error after setting extent: {ex.Message}");
                }
            });

            // The NotifyPropertyChanged calls for UseCustomExtent and UseCurrentMapExtent
        } // <--- Added missing right curly bracket here

        private async Task CreateMfcAsync()
        {
            try
            {
                // Initialize a new cancellation token source
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                var cancellationToken = _cts.Token;

                // Switch to status tab
                SelectedTabIndex = 1; // Status tab is now index 1

                StatusText = "Creating Multifile Feature Connection...";
                AddToLog("为数据设置多文件要素连接");
                ProgressValue = 0;

                // Determine the data source folder
                string dataFolder;

                if (UsePreviouslyLoadedData && !string.IsNullOrEmpty(_lastLoadedDataPath))
                {
                    dataFolder = _lastLoadedDataPath;
                    AddToLog($"使用之前加载的数据: {dataFolder}");
                }
                else if (UseCustomDataFolder && !string.IsNullOrEmpty(CustomDataFolderPath))
                {
                    dataFolder = CustomDataFolderPath;
                    AddToLog($"使用自定义数据文件夹: {dataFolder}");
                }
                else
                {
                    StatusText = "Error: No valid data folder specified";
                    AddToLog("ERROR: No valid data folder specified for MFC creation");
                    return;
                }

                var dataFolderInspector = _mfcDataFolderInspector
                    ?? throw new InvalidOperationException("MFC 数据文件夹检查服务未初始化。");

                // Ensure connection folder exists
                string connectionFolder = MfcOutputPath;
                if (await dataFolderInspector.EnsureOutputFolderAsync(connectionFolder, cancellationToken))
                {
                    AddToLog($"Creating connection folder: {connectionFolder}");
                }

                // Check if data folder exists and has content
                var folderInspection = await dataFolderInspector.InspectAsync(dataFolder, cancellationToken);
                if (!folderInspection.Exists)
                {
                    AddToLog($"ERROR: Data folder does not exist: {dataFolder}");
                    StatusText = "Error creating Multifile Feature Connection - data folder not found";
                    return;
                }

                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    StatusText = "Operation cancelled";
                    AddToLog("Operation was cancelled during MFC preparation");
                    return;
                }

                // Do a sanity check on the data folder contents
                int fileCount = folderInspection.ParquetFileCount;
                AddToLog($"在数据文件夹中找到 {fileCount} 个parquet文件");

                if (fileCount == 0)
                {
                    // Check if theme folders were created
                    var themeFolders = folderInspection.ThemeFolders;
                    AddToLog($"Found {themeFolders.Count} theme folders in {dataFolder}");

                    foreach (var folder in themeFolders)
                    {
                        AddToLog($"Theme folder: {folder.Name}");
                        AddToLog($"  Contains {folder.TypeFolders.Count} type folders");

                        foreach (var typeFolder in folder.TypeFolders)
                        {
                            AddToLog($"  Type folder {typeFolder.Name} contains {typeFolder.ParquetFileCount} parquet files");
                        }
                    }

                    if (fileCount == 0)
                    {
                        AddToLog("在指定文件夹中未找到数据文件。无法创建MFC。");
                        StatusText = "Error creating Multifile Feature Connection - no data files found";
                        return;
                    }
                }

                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    StatusText = "Operation cancelled";
                    AddToLog("Operation was cancelled before MFC creation");
                    return;
                }

                // Create a nice MFC filename based on the release and sanitize it
                string releaseName = LatestRelease?.Replace("-", "") ?? "Latest";
                string mfcName = $"OvertureRelease_{releaseName}";
                string mfcFilePath = Path.Combine(connectionFolder, $"{mfcName}.mfc");

                AddToLog($"MFC源文件夹: {dataFolder}");
                AddToLog($"MFC输出位置: {connectionFolder}");
                AddToLog($"MFC名称: {mfcName}");

                ProgressValue = 30; // Show progress starting

                try
                {
                    // Get the add-in's execution path to help locate bundled DuckDB extensions
                    string addinExecutionPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                    bool success = await Services.MfcUtility.GenerateMfcFileAsync(
                        dataFolder,         // Source folder with the properly structured datasets
                        mfcFilePath,        // Full path to the output MFC file
                        addinExecutionPath, // Path to the add-in's executing directory
                        (logMessage) => AddToLog(logMessage), // Pass the AddToLog method for logging within the utility
                        cancellationToken
                    );

                    // Check for cancellation
                    if (cancellationToken.IsCancellationRequested)
                    {
                        StatusText = "Operation cancelled";
                        AddToLog("Operation was cancelled during MFC creation");
                        return;
                    }

                    ProgressValue = 100; // Complete

                    if (success)
                    {
                        StatusText = "Successfully created Multifile Feature Connection";
                        AddToLog($"MFC创建于: {mfcFilePath}");

                        // Provide simplified instructions for adding the MFC to the project
                        try
                        {
                            AddToLog("----------------");
                            AddToLog("在项目中使用MFC:");
                            AddToLog("1. 在目录窗格中，导航到MFC文件的位置");
                            AddToLog($"2. 右键单击文件: {Path.GetFileName(mfcFilePath)}");
                            AddToLog("3. 选择 '添加到项目'");
                            AddToLog("4. MFC将出现在 '多文件要素连接' 部分");
                            AddToLog("----------------");

                            // Display a message box with instructions
                            PresentationServices.Dialogs.Show(
                                $"多文件要素连接创建成功！\n\n" +
                                $"位置: {mfcFilePath}\n\n" +
                                "要将其添加到项目中：\n" +
                                "1. 在目录窗格中导航到MFC文件\n" +
                                $"2. 右键单击 '{Path.GetFileName(mfcFilePath)}'\n" +
                                "3. 选择 '添加到项目'",
                                "MFC创建成功",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            AddToLog($"Warning: {ex.Message}");
                        }
                    }
                    else
                    {
                        StatusText = "Error creating Multifile Feature Connection";
                        AddToLog("创建MFC失败。请查看ArcGIS Pro日志了解详细信息。");

                        // Show a message box with more details to help the user
                        PresentationServices.Dialogs.Show(
                            "无法创建多文件要素连接。\n\n" +
                            "可能的原因：\n" +
                            "1. 在预期的文件夹结构中未找到数据文件\n" +
                            "2. GeoParquet文件结构不正确\n" +
                            "3. ArcGIS Pro没有创建MFC文件的权限\n\n" +
                            "请查看日志选项卡了解更多详细信息。",
                            "MFC创建失败",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    StatusText = "Error creating Multifile Feature Connection";
                    AddToLog($"ERROR: Exception creating MFC: {ex.Message}");
                    AddToLog($"Stack trace: {ex.StackTrace}");
                    ProgressValue = 0;
                }
            }
            catch (OperationCanceledException) when (_cts?.IsCancellationRequested == true)
            {
                StatusText = "Operation cancelled";
                AddToLog("Operation was cancelled during MFC preparation");
            }
            catch (Exception ex)
            {
                StatusText = $"Error creating MFC: {ex.Message}";
                AddToLog($"ERROR: {ex.Message}");
                AddToLog($"Stack trace: {ex.StackTrace}");
                ProgressValue = 0;
                System.Diagnostics.Debug.WriteLine($"MFC creation error: {ex}");
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

        private void ShowCreateMfcTab()
        {
            // Navigate to the Create MFC tab (index 2)
            SelectedTabIndex = 2;
            StatusText = "准备创建多文件要素连接";
            AddToLog("创建MFC选项卡已激活");
        }
    }
}
