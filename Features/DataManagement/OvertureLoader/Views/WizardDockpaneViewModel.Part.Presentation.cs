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
        private void AddToLog(string message)
        {
            // Append the new log entry to the end of the log
            LogOutput.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");

            // Update the text property
            LogOutputText = LogOutput.ToString();
            NotifyPropertyChanged(nameof(LogOutputText));
        }

        private void UpdateThemePreview()
        {
            string description = "选择一个主题或子主题以查看详细信息。";
            string icon = "GlobeIcon"; // Default

            var itemForPreview = SelectedItemForPreview; // The item currently focused in TreeView

            if (itemForPreview != null)
            {
                // Get description and icon from the parent theme typically
                string parentS3Key = itemForPreview.IsSelectable && itemForPreview.SubItems.Count == 0 && Themes.Any(t => t.ActualType == itemForPreview.ParentThemeForS3 && t.SubItems.Count == 0) ?
                                     itemForPreview.ActualType : // If it's a leaf parent (like "places")
                                     itemForPreview.ParentThemeForS3; // Otherwise, use the parent key

                description = ThemeDescriptions.TryGetValue(parentS3Key, out var desc)
                    ? desc
                    : "无描述信息。";

                if (itemForPreview.IsSelectable && itemForPreview.ParentThemeForS3 != itemForPreview.ActualType) // It's a sub-item
                {
                    description += "\nSub-theme: " + itemForPreview.DisplayName;
                }

                icon = ThemeIcons.TryGetValue(parentS3Key, out var iconName) ? iconName : "GlobeIcon";
            }

            // Calculate combined estimates for ALL selected leaf themes
            var allSelectedLeaves = GetSelectedLeafItems();
            if (allSelectedLeaves.Count > 0)
            {
                int totalEstimatedFeatures = 0;
                double totalSizeInKb = 0;

                foreach (var selectedLeaf in allSelectedLeaves)
                {
                    // Use the ActualType of the leaf item to get its specific estimate
                    if (ThemeFeatureEstimates.TryGetValue(selectedLeaf.ActualType, out int itemEstimate))
                    {
                        totalEstimatedFeatures += itemEstimate;
                        totalSizeInKb += itemEstimate * 2.5; // Assuming 2.5KB per feature
                    }
                    else
                    {
                        // Optional: Log if an estimate is missing for an actual type
                        System.Diagnostics.Debug.WriteLine($"Warning: No feature estimate found for ActualType: {selectedLeaf.ActualType}");
                    }
                }
                EstimatedFeatures = $"{totalEstimatedFeatures} total per sq km (approx.)";
                EstimatedSize = totalSizeInKb > 1024
                    ? $"{totalSizeInKb / 1024:F1} MB total per sq km (approx.)"
                    : $"{totalSizeInKb:F0} KB total per sq km (approx.)";

                if (allSelectedLeaves.Count == 1 && itemForPreview != null && itemForPreview == allSelectedLeaves.First()) // If only one item is selected, and it's the one being previewed
                {
                    // Use the ActualType of the itemForPreview to get its specific estimate
                    if (ThemeFeatureEstimates.TryGetValue(itemForPreview.ActualType, out int itemFeatures))
                    {
                        double itemSizeKb = itemFeatures * 2.5;
                        EstimatedFeatures = $"{itemFeatures} per sq km (approx. for {itemForPreview.DisplayName})";
                        EstimatedSize = itemSizeKb > 1024
                            ? $"{itemSizeKb / 1024:F1} MB per sq km (approx. for {itemForPreview.DisplayName})"
                            : $"{itemSizeKb:F0} KB per sq km (approx. for {itemForPreview.DisplayName})";
                    }
                    else
                    {
                        // Fallback if specific estimate is missing for the single selected item
                        EstimatedFeatures = $"-- per sq km (approx. for {itemForPreview.DisplayName})";
                        EstimatedSize = $"-- MB/KB per sq km (approx. for {itemForPreview.DisplayName})";
                        System.Diagnostics.Debug.WriteLine($"Warning: No feature estimate for single selected ActualType: {itemForPreview.ActualType}");
                    }
                }
            }
            else // No items are selected
            {
                // If nothing is selected, but an item is focused for preview, show its individual estimate
                if (itemForPreview != null && itemForPreview.IsSelectable) // Check if the preview item is a selectable leaf
                {
                    // Use the ActualType of the itemForPreview to get its specific estimate
                    if (ThemeFeatureEstimates.TryGetValue(itemForPreview.ActualType, out int itemFeat))
                    {
                        double itemSzKb = itemFeat * 2.5;
                        EstimatedFeatures = $"{itemFeat} per sq km (approx. for {itemForPreview.DisplayName})";
                        EstimatedSize = itemSzKb > 1024
                            ? $"{itemSzKb / 1024:F1} MB per sq km (approx. for {itemForPreview.DisplayName})"
                            : $"{itemSzKb:F0} KB per sq km (approx. for {itemForPreview.DisplayName})";
                    }
                    else
                    {
                        // Fallback if specific estimate is missing for the focused item
                        EstimatedFeatures = $"-- per sq km (approx. for {itemForPreview.DisplayName})";
                        EstimatedSize = $"-- MB/KB per sq km (approx. for {itemForPreview.DisplayName})";
                        System.Diagnostics.Debug.WriteLine($"Warning: No feature estimate for focused ActualType: {itemForPreview.ActualType}");
                    }
                }
                else // Nothing selected and no specific leaf item focused for preview
                {
                    EstimatedFeatures = "--";
                    EstimatedSize = "--";
                }
            }

            ThemeDescription = description;
            ThemeIconText = icon;
            // EstimatedFeatures and EstimatedSize are set above
            NotifyPropertyChanged(nameof(ThemeDescription));
            NotifyPropertyChanged(nameof(EstimatedFeatures));
            NotifyPropertyChanged(nameof(EstimatedSize));
            NotifyPropertyChanged(nameof(ThemeIconText));
            NotifyPropertyChanged(nameof(SelectedLeafItemCount));
            NotifyPropertyChanged(nameof(AllSelectedLeafItemsForPreview));
            UpdateIsSelectAllCheckedStatus(); // Ensure "Select All" checkbox reflects current state
        }

        private void ShowThemeInfo()
        {
            if (SelectedItemForPreview == null) return;

            var item = SelectedItemForPreview;
            string parentS3Key = item.ParentThemeForS3;

            string description = ThemeDescriptions.TryGetValue(parentS3Key, out string themeDesc)
                ? themeDesc
                : "无详细信息可用。";

            string typesInfo = $"S3 主题: {parentS3Key}";
            if (item.IsSelectable && item.ParentThemeForS3 != item.ActualType) // It's a sub-item
            {
                description = $"父级: {MakeFriendlyName(parentS3Key)}\n子主题: {item.DisplayName}\n\n{description}";
                typesInfo += $", S3 类型: {item.ActualType}";
            }
            else // It's a parent item (either leaf or just for preview)
            {
                typesInfo += $", S3 类型: {_overtureS3ThemeTypes[parentS3Key]}";
            }

            var selectedLeafItems = GetSelectedLeafItems();
            string selectedCount = selectedLeafItems.Count > 0 ?
                $"\n\nYou have selected {selectedLeafItems.Count} specific data type(s) in total."
                : "";

            PresentationServices.Dialogs.Show(
                $"{description}\n\n{typesInfo}{selectedCount}",
                $"关于 '{item.DisplayName}'",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void ShowHelp()
        {
            string helpMessage = @"关于 Overture Maps 数据加载器

这是一个用于加载和处理 Overture Maps 数据的工具，基于开源项目进行改进：
https://github.com/COF-RyLopez/ArcGISPro-GeoParquet-Addin

主要功能：
• 从 Overture Maps 下载 GeoParquet 格式的地理数据
• 支持多种数据主题：建筑物、地点、交通、行政边界等
• 创建多文件要素连接 (MFC) 以便在 ArcGIS Pro 中使用
• 自定义数据范围和输出位置

使用步骤：
1. 选择数据主题和类型
2. 设置数据范围（当前地图范围或自定义范围）
3. 点击'加载数据'下载并处理数据
4. 可选择创建 MFC 文件以便重复使用

注意事项：
• 需要稳定的网络连接来下载数据
• 大范围数据可能需要较长时间处理
• 建议先在小范围内测试

技术支持：
如有问题，请查看 ArcGIS Pro 日志或联系开发团队。

版权信息：
基于 COF-RyLopez 的开源项目改进
集成到 XIAOFU 工具箱中";

            PresentationServices.Dialogs.Show(
                helpMessage,
                "Overture Maps 数据加载器 - 帮助",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void BrowseMfcLocation()
        {
            var initial = MfcOutputPath ?? Path.Combine(DeterminedDefaultMfcBasePath, "Connections");
            var picked = PathDialogUtils.PickFolder("选择MFC文件(.mfc)的保存文件夹", initial);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                MfcOutputPath = picked;
                AddToLog($"MFC连接文件将保存在: {MfcOutputPath}");
                AddToLog($"请确保您的GeoParquet数据文件位于: {DataOutputPath}");
            }
        }

        private void BrowseDataLocation()
        {
            var initial = DataOutputPath ?? Path.Combine(DeterminedDefaultMfcBasePath, "Data");
            var picked = PathDialogUtils.PickFolder("选择GeoParquet数据文件的保存文件夹", initial);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                DataOutputPath = picked;
                AddToLog($"数据文件将保存到: {DataOutputPath}");
            }
        }

        private void BrowseCustomDataFolder()
        {
            var initial = CustomDataFolderPath ?? DataOutputPath ?? Path.Combine(DeterminedDefaultMfcBasePath, "Data");
            var picked = PathDialogUtils.PickFolder("选择包含GeoParquet数据文件的文件夹", initial);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                CustomDataFolderPath = picked;
                AddToLog($"自定义数据文件夹设置为: {CustomDataFolderPath}");
                (CreateMfcCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void UpdateCustomExtentDisplay()
        {
            if (_customExtent != null)
            {
                CustomExtentDisplay = $"Min X: {_customExtent.XMin:F4}\nMin Y: {_customExtent.YMin:F4}\nMax X: {_customExtent.XMax:F4}\nMax Y: {_customExtent.YMax:F4}";
            }
            else
            {
                CustomExtentDisplay = "未设置自定义范围";
            }
        }

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            // Reset the state when showing the dockpane
            if (pane is WizardDockpaneViewModel viewModel)
            {
                viewModel.ResetState();
            }

            pane.Activate();
        }

        public bool IsThemeSelected(string theme)
        {
            var themeItem = Themes.FirstOrDefault(t => t.DisplayName == theme);
            return themeItem != null && themeItem.IsSelected == true; // Corrected: bool? to bool comparison
        }

        public void ToggleThemeSelection(string theme)
        {
            var themeItem = Themes.FirstOrDefault(t => t.DisplayName == theme);
            if (themeItem != null)
            {
                themeItem.IsSelected = !themeItem.IsSelected;
                // The OnThemeSelectionChanged event handler will update SelectedThemes
            }
        }

        // Add a method to check the selected status in the ViewModel
        private void CheckInitialThemeSelection()
        {
            // Update the preview based on the first selected theme (if any)
            if (Themes.Any())
            {
                SelectedTheme = Themes[0].DisplayName;
            }
        }

        // This event handler is for the original flat list of themes. 
        // It's superseded by OnLeafThemeSelectionChanged for hierarchical themes.
        // Consider removing or refactoring if only hierarchical selection is used.
        private void OnThemeSelectionChanged(object sender, EventArgs e)
        {
            // Update the SelectedThemes list based on the currently selected theme items
            // _selectedThemes.Clear(); // No longer used
            // foreach (var themeItem in Themes)
            // {
            //     if (themeItem.IsSelected)
            //     {
            //         _selectedThemes.Add(themeItem.DisplayName); // No longer used
            //     }
            // }

            // NotifyPropertyChanged(nameof(SelectedThemes)); // No longer used
            UpdateThemePreview();
            (LoadDataCommand as RelayCommand)?.RaiseCanExecuteChanged();

            // If a theme was selected, set it as the current preview theme
            if (sender is SelectableThemeItem selectedItem && selectedItem.IsSelected == true) // Corrected: bool? to bool comparison
            {
                SelectedTheme = selectedItem.DisplayName; // This might still be useful for a general preview
                                                          // but SelectedItemForPreview is now primary for TreeView focus
            }
            // else if (_selectedThemes.Count > 0) // No longer used
            // {
            //     // If we just deselected an item but others are still selected, show the first selected theme
            //     SelectedTheme = _selectedThemes[0]; // No longer used
            // }
            else if (GetSelectedLeafItems().Count > 0) // CA1860 .Any() to .Count > 0 // If deselected, but other leaves are selected
            {
                SelectedTheme = GetSelectedLeafItems().First().ParentThemeForS3; // Or another suitable property
            }
            else
            {
                // If no themes are selected, clear the selection
                SelectedTheme = null;
            }
        }

        // This method is now the primary handler for selection changes on leaf items
        private void OnLeafThemeSelectionChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSelectionInternally) return; // Skip if a bulk update is in progress

            if (sender is SelectableThemeItem selectedLeafItem)
            {
                // Set this item for preview purposes, even if it's being deselected
                // The preview panel will update based on this item's state and overall selections
                SelectedItemForPreview = selectedLeafItem;
            }
            // Update combined estimates and other UI elements that depend on the full selection set
            UpdateThemePreview(); // This eventually calls UpdateIsSelectAllCheckedStatus
            (LoadDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(SelectedLeafItemCount));
            NotifyPropertyChanged(nameof(AllSelectedLeafItemsForPreview));
            // UpdateIsSelectAllCheckedStatus(); // Explicitly call to ensure status is current
        }

        private void ExecuteSelectAllInternal(bool select)
        {
            if (Themes == null) return;

            _isUpdatingSelectionInternally = true;
            try
            {
                foreach (var themeItem in Themes)
                {
                    if (themeItem.IsSelectable) // Parent is a leaf
                    {
                        themeItem.IsSelected = select;
                    }
                    else if (themeItem.SubItems.Any()) // Parent has sub-items, set its state (will propagate)
                    {
                        themeItem.IsSelected = select; // This will trigger propagation to children
                    }
                    // No need to iterate sub-items here anymore, parent IsSelected setter handles it.

                    // Expand/Collapse parent themes based on 'select' state
                    if (themeItem.IsExpandable)
                    {
                        themeItem.IsExpanded = select; // Set to true if select is true, false if select is false
                    }
                }
            }
            finally
            {
                _isUpdatingSelectionInternally = false;
            }

            // After bulk update, the individual OnLeafThemeSelectionChanged handlers were skipped.
            // We need to manually trigger updates for dependent properties and the overall "Select All" state.
            UpdateThemePreview(); // Refreshes previews, and calls UpdateIsSelectAllCheckedStatus
            (LoadDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(SelectedLeafItemCount));
            NotifyPropertyChanged(nameof(AllSelectedLeafItemsForPreview));
            // UpdateIsSelectAllCheckedStatus(); // Called by UpdateThemePreview indirectly, but call directly for safety
        }
    }
}
