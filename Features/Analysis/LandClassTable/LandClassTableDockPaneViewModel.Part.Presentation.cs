using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    internal sealed partial class LandClassTableDockPaneViewModel
    {

        private void NotifyCanProcessChanged()
        {
            NotifyPropertyChanged(() => CanProcess);
            _refreshLayersCommand?.RaiseCanExecuteChanged();
            _browseOutputCommand?.RaiseCanExecuteChanged();
            _manageReportProfilesCommand?.RaiseCanExecuteChanged();
            _runCommand?.RaiseCanExecuteChanged();
            _cancelCommand?.RaiseCanExecuteChanged();
            CommandManager.InvalidateRequerySuggested();
        }


        private void UpdateSelectedProfile(Action<LandClassReportProfile> update)
        {
            if (SelectedReportProfile != null)
            {
                update(SelectedReportProfile);
            }
        }


        public void RefreshLayers()
        {
            Task.Run(async () =>
            {
                try
                {
                    var layers = new List<FeatureLayer>();
                    await QueuedTask.Run(() =>
                    {
                        var map = MapView.Active?.Map;
                        if (map == null)
                        {
                            return;
                        }

                        foreach (var layer in map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
                        {
                            var featureClass = layer.GetFeatureClass();
                            if (featureClass?.GetDefinition()?.GetShapeType() == GeometryType.Polygon)
                            {
                                layers.Add(layer);
                            }
                        }
                    });

                    PresentationServices.UiThread.Invoke(() =>
                    {
                        PolygonLayers.Clear();
                        foreach (var layer in layers)
                        {
                            PolygonLayers.Add(layer);
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogError($"加载图层失败: {ex.Message}");
                }
            });
        }


        private void BrowseOutput()
        {
            var picked = PathDialogUtils.PickFolder("选择地类表输出文件夹", OutputFolder);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                OutputFolder = picked;
            }
        }


        private static void ThrowIfCancelled(Func<bool> isCancelRequested)
        {
            if (isCancelRequested())
            {
                throw new OperationCanceledException();
            }
        }


        private void ShowHelp()
        {
            PresentationServices.Dialogs.Show(
                "按项目红线与地类图斑叠加生成地类表。\n\n" +
                "项目红线：选择面图层和面积字段，面积字段值作为最终总计面积；名称字段可留空，默认取图层名。\n" +
                "地类图斑：选择面图层、地类名称或编码字段；权属来源可自动读取字段，也可手动填写。\n" +
                "自动权属：默认识别 QSDWMC 和 QSXZ。\n" +
                "权属性质：10/20 识别为国有，30/40 识别为集体。\n" +
                "分组输出：选择分组字段后按字段值输出 地类表_字段值.xlsx。\n" +
                "地块名称：选择地块名称字段后在序号后增加地块名称列。\n" +
                "参数信息：可通过“设置”按钮保存年度、所在地、填报单位、填表人、审核员。\n" +
                "输出文件夹：从零创建 .xlsx。",
                "地类表",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }


        private void UpdateProgress(int value)
        {
            PresentationServices.UiThread.Invoke(() =>
            {
                IsProgressIndeterminate = false;
                Progress = Math.Max(0, Math.Min(100, value));
            });
        }


        private void UpdateProgressFromWorker(int value)
        {
            PresentationServices.UiThread.Post(() =>
            {
                IsProgressIndeterminate = false;
                Progress = Math.Max(0, Math.Min(100, value));
            });
        }


        private void LogInfo(string message) => AppendLog(message, "信息");

        private void LogWarning(string message) => AppendLog(message, "警告");

        private void LogError(string message) => AppendLog(message, "错误");


        private void AppendLog(string message, string level)
        {
            PresentationServices.UiThread.Invoke(() =>
            {
                LogContent += $"[{DateTime.Now:HH:mm:ss}] {level}: {message}{Environment.NewLine}";
            });
        }

    }
}
