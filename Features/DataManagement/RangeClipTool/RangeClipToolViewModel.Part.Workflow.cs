using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Shared;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.DataManagement.RangeClipTool
{
    internal partial class RangeClipToolViewModel
    {

        /// <summary>
        /// 确定命令是否可以执行
        /// </summary>
        private bool CanExecute()
        {
            return SelectedRangeLayer != null &&
                   !string.IsNullOrEmpty(SelectedRangeField) &&
                   ClipLayerItems.Any(x => x.IsSelected) &&
                   !string.IsNullOrEmpty(OutputFolder) &&
                   _outputFolderStore.DirectoryExists(OutputFolder);
        }

        /// <summary>
        /// 执行裁剪操作
        /// </summary>
        private async void Execute()
        {
            if (!CanExecute())
            {
                StatusMessage = "请检查参数设置。";
                return;
            }

            IsProcessing = true;
            CancelRequested = false;
            Progress = 0;
            IsProgressIndeterminate = true;

            try
            {
                StatusMessage = "正在执行根据范围批量裁剪...";
                LogInfo("开始执行根据范围批量裁剪操作");

                await QueuedTask.Run(async () =>
                {
                    await PerformRangeClip();
                });

                if (!CancelRequested)
                {
                    StatusMessage = "根据范围批量裁剪完成。";
                    LogInfo("根据范围批量裁剪操作完成");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"执行出错: {ex.Message}";
                LogError($"执行出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                Progress = 0;
            }
        }
    }
}
