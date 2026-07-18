using System;
using System.ComponentModel;
using System.Windows.Input;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Features.User.PluginUpdate;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.User.Settings
{
    public partial class SettingsDockPaneViewModel
    {

        /// <summary>
        /// 导入预设图层
        /// </summary>
        private void ImportLayers()
        {
            try
            {
                var selectedFiles = PresentationServices.Files.OpenFiles(
                    "图层文件 (*.lyr;*.lyrx)|*.lyr;*.lyrx|ArcMap图层文件 (*.lyr)|*.lyr|ArcGIS Pro图层文件 (*.lyrx)|*.lyrx",
                    title: "选择预设图层文件");

                if (selectedFiles.Count > 0)
                {
                    string targetPath = LayersPath;

                    _presetLayerFileStore.EnsureDirectory(targetPath);

                    int successCount = 0;
                    int failCount = 0;

                    foreach (string sourceFile in selectedFiles)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(sourceFile);
                            string destFile = Path.Combine(targetPath, fileName);

                            // 如果文件已存在，询问是否覆盖
                            if (_presetLayerFileStore.FileExists(destFile))
                            {
                                var result = PresentationServices.Dialogs.Show(
                                    $"文件 '{fileName}' 已存在，是否覆盖？",
                                    "文件已存在",
                                    System.Windows.MessageBoxButton.YesNo,
                                    System.Windows.MessageBoxImage.Question);

                                if (result != System.Windows.MessageBoxResult.Yes)
                                {
                                    continue;
                                }
                            }

                            _presetLayerFileStore.CopyFile(sourceFile, destFile);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"复制文件失败: {ex.Message}");
                            failCount++;
                        }
                    }

                    string message = $"导入完成！\n成功: {successCount} 个文件";
                    if (failCount > 0)
                    {
                        message += $"\n失败: {failCount} 个文件";
                    }

                    PresentationServices.Dialogs.Show(
                        message,
                        "导入结果",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show(
                    $"导入预设图层时出错: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
