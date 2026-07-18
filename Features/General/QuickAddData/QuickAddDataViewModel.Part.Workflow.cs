using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Win32;

namespace XIAOFUTools.Features.General.QuickAddData
{
    internal sealed partial class QuickAddDataViewModel
    {

        private void ImportFilesIntoGroup(QuickDataGroup targetGroup)
        {
            var selectedFiles = PresentationServices.Files.OpenFiles(
                "GIS数据|*.shp;*.lyr;*.lyrx;*.tif;*.tiff;*.img;*.jpg;*.jpeg;*.png;*.bmp|全部文件|*.*",
                title: "选择要加入快捷库的数据文件");

            if (selectedFiles.Count == 0)
            {
                return;
            }

            _ = ImportIntoGroupAsync(targetGroup, selectedFiles, "文件");
        }


        private void ImportFolderIntoGroup(QuickDataGroup targetGroup)
        {
            var folderPath = XIAOFUTools.Shared.PathDialogUtils.PickFolder("选择要导入的文件夹或 GDB");
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            _ = ImportIntoGroupAsync(targetGroup, new[] { folderPath }, "目录");
        }


        private async Task ImportIntoGroupAsync(QuickDataGroup targetGroup, IEnumerable<string> inputPaths, string originLabel)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var importedNodes = await Task.Run(() => _importService.ImportPaths(inputPaths, new QuickDataImportOptions
                {
                    IncludeFeatureClasses = true,
                    IncludeTables = true,
                    IncludeRasters = true,
                    IncludeLayerFiles = true,
                    Recurse = true
                }));

                if (importedNodes.Count == 0)
                {
                    StatusMessage = $"没有从{originLabel}中识别出可加入快捷库的数据。";
                    return;
                }

                var detector = new QuickDataDuplicateDetector(EnumerateNodes(targetGroup.Nodes).Select(node => node.UniqueKey));
                var addedCount = 0;
                var skippedCount = 0;

                foreach (var node in importedNodes.OrderBy(node => node.Name))
                {
                    if (detector.TryRegister(node))
                    {
                        targetGroup.Nodes.Add(node.CloneDeep());
                        addedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                PersistLibrary($"导入完成，新增 {addedCount}，跳过 {skippedCount}。");
            }
            catch (Exception ex)
            {
                StatusMessage = $"导入{originLabel}失败：{ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

    }
}
