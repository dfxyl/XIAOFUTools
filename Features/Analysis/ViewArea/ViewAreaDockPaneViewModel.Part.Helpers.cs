using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Features.User.Settings;

namespace XIAOFUTools.Features.Analysis.ViewArea
{
    internal partial class ViewAreaDockPaneViewModel
    {

        /// <summary>
        /// 清理事件订阅
        /// </summary>
        public void Cleanup()
        {
            if (_mapSelectionChangedToken != null)
            {
                MapSelectionChangedEvent.Unsubscribe(_mapSelectionChangedToken);
                _mapSelectionChangedToken = null;
            }
        }

        /// <summary>
        /// 复制结果到剪贴板（带重试机制）
        /// </summary>
        private async void CopyResults()
        {
            try
            {
                var result = new System.Text.StringBuilder();

                // 添加标题
                result.AppendLine("面积/长度计算结果");
                result.AppendLine("=" + new string('=', 30));
                result.AppendLine();

                // 添加选择信息
                result.AppendLine(SelectionInfo);
                result.AppendLine();

                // 合并计算结果
                if (HasResults)
                {
                    result.AppendLine("合并计算结果:");
                    result.AppendLine("-" + new string('-', 20));

                    // 面积结果
                    if (HasAreaResults)
                    {
                        result.AppendLine("面积:");
                        foreach (var areaResult in CombinedAreaResults)
                        {
                            result.AppendLine($"  {areaResult.CalculationType}:");
                            result.AppendLine($"    平方米: {areaResult.Unit1Value}");
                            result.AppendLine($"    公顷: {areaResult.Unit2Value}");
                            result.AppendLine($"    亩: {areaResult.Unit3Value}");
                            result.AppendLine($"    平方公里: {areaResult.Unit4Value}");
                        }
                        result.AppendLine();
                    }

                    // 长度结果
                    if (HasLengthResults)
                    {
                        result.AppendLine("长度:");
                        foreach (var lengthResult in CombinedLengthResults)
                        {
                            result.AppendLine($"  {lengthResult.CalculationType}:");
                            result.AppendLine($"    米: {lengthResult.Unit1Value}");
                            result.AppendLine($"    千米: {lengthResult.Unit2Value}");
                        }
                        result.AppendLine();
                    }

                    // 分图层结果
                    if (ShowLayerResults)
                    {
                        result.AppendLine("分图层计算结果:");
                        result.AppendLine("-" + new string('-', 20));

                        foreach (var layerResult in LayerResults)
                        {
                            result.AppendLine($"{layerResult.LayerName} ({layerResult.FeatureCount} 个要素):");

                            if (layerResult.HasAreaResults)
                            {
                                result.AppendLine("  面积:");
                                foreach (var areaResult in layerResult.AreaResults)
                                {
                                    result.AppendLine($"    {areaResult.CalculationType}: {areaResult.Unit1Value}㎡, {areaResult.Unit2Value}公顷, {areaResult.Unit3Value}亩, {areaResult.Unit4Value}k㎡");
                                }
                            }

                            if (layerResult.HasLengthResults)
                            {
                                result.AppendLine("  长度:");
                                foreach (var lengthResult in layerResult.LengthResults)
                                {
                                    result.AppendLine($"    {lengthResult.CalculationType}: {lengthResult.Unit1Value}米, {lengthResult.Unit2Value}千米");
                                }
                            }
                            result.AppendLine();
                        }
                    }
                }
                else
                {
                    result.AppendLine("无计算结果");
                }

                // 添加说明
                result.AppendLine();
                result.AppendLine("说明:");
                result.AppendLine("椭球面积和测地线长度使用ArcGIS Pro内置高精度算法计算");
                result.AppendLine("计算策略：无坐标系-平面计算；地理坐标系-椭球计算；投影坐标系-两种都计算");

                // 使用重试机制复制到剪贴板
                var success = await PresentationServices.Clipboard.TrySetTextAsync(result.ToString());

                if (success)
                {
                    // 显示成功消息
                    PresentationServices.Dialogs.Show("计算结果已复制到剪贴板", "复制成功",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    // 复制失败，提供备用方案
                    ShowCopyFailureDialog(result.ToString());
                }
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show($"复制失败: {ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清空结果
        /// </summary>
        private void ClearResults()
        {
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                CombinedAreaResults.Clear();
                CombinedLengthResults.Clear();
                LayerResults.Clear();
                HasAreaResults = false;
                HasLengthResults = false;
                ShowLayerResults = false;
                NotifyPropertyChanged(() => HasResults);
            });
        }

        /// <summary>
        /// 确定坐标系策略
        /// </summary>
        private CoordinateSystemType DetermineCoordinateSystemStrategy(List<CoordinateSystemType> coordinateSystemTypes)
        {
            if (!coordinateSystemTypes.Any())
                return CoordinateSystemType.None;

            // 如果所有图层坐标系类型一致，使用该类型
            var firstType = coordinateSystemTypes.First();
            if (coordinateSystemTypes.All(t => t == firstType))
                return firstType;

            // 如果坐标系类型不一致，优先级：投影坐标系 > 地理坐标系 > 无坐标系
            if (coordinateSystemTypes.Any(t => t == CoordinateSystemType.Projected))
                return CoordinateSystemType.Projected;

            if (coordinateSystemTypes.Any(t => t == CoordinateSystemType.Geographic))
                return CoordinateSystemType.Geographic;

            return CoordinateSystemType.None;
        }
    }
}
