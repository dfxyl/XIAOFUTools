using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.IO;
using System.Linq;

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.OverlapCheck
{
    internal partial class OverlapCheckDockPaneViewModel
    {
        private void InitializeCommands()
        {
            BrowseOutputCommand = new RelayCommand(BrowseOutput);
            RunCommand = new RelayCommand(async () => await RunOverlapCheck(), () => CanProcess);
            CancelCommand = new RelayCommand(CancelProcess);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            SelectFieldsCommand = new RelayCommand(SelectFields);
            RefreshLayersCommand = new RelayCommand(RefreshLayers);
        }

        private void InitializeData()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            AddLog("图形重叠检查工具已启动");
        }

        /// <summary>
        /// 初始化界面
        /// </summary>
        public void Initialize()
        {
            LoadPolygonLayers();
        }

        /// <summary>
        /// 插入重叠区域要素
        /// </summary>
        private async Task InsertOverlapFeatures(string outputFeatureClassPath, List<OverlapInfo> overlaps, CancellationToken cancellationToken)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var workspace = Path.GetDirectoryName(outputFeatureClassPath);
                    var featureClassName = Path.GetFileNameWithoutExtension(outputFeatureClassPath);

                    if (string.IsNullOrEmpty(workspace) || string.IsNullOrEmpty(featureClassName))
                    {
                        AddLog("无效的输出要素类路径");
                        return;
                    }

                    using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace))))
                    using (var featureClass = geodatabase.OpenDataset<FeatureClass>(featureClassName))
                    {
                        int insertedCount = 0;
                        double tolerance = double.TryParse(Tolerance, out double t) ? t : 0.001;

                        for (int i = 0; i < overlaps.Count; i++)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            try
                            {
                                var overlapInfo = overlaps[i];
                                if (overlapInfo.Geometry is Polygon polygon)
                                {
                                    using (var rowBuffer = featureClass.CreateRowBuffer())
                                    {
                                        // 设置几何
                                        rowBuffer[featureClass.GetDefinition().GetShapeField()] = polygon;

                                        // 设置基本属性
                                        try
                                        {
                                            rowBuffer["OVERLAP_ID"] = i + 1;
                                            rowBuffer["AREA"] = polygon.Area;
                                            rowBuffer["TOLERANCE"] = tolerance;

                                            // 设置保留字段的值
                                            if (SelectedFields != null && SelectedFields.Count > 0)
                                            {
                                                foreach (var fieldName in SelectedFields)
                                                {
                                                    try
                                                    {
                                                        if (overlapInfo.Feature1Fields.ContainsKey(fieldName))
                                                        {
                                                            rowBuffer[fieldName] = overlapInfo.Feature1Fields[fieldName];
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        AddLog($"设置保留字段 {fieldName} 失败: {ex.Message}");
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            AddLog($"设置字段值失败: {ex.Message}");
                                        }

                                        using (var feature = featureClass.CreateRow(rowBuffer))
                                        {
                                            feature.Store();
                                            insertedCount++;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                AddLog($"插入要素 {i + 1} 失败: {ex.Message}");
                            }

                            // 更新进度
                            if (i % 10 == 0)
                            {
                                Progress = (int)((double)(i + 1) / overlaps.Count * 100);
                            }
                        }

                        AddLog($"成功插入 {insertedCount} 个重叠区域要素");
                    }
                });
            }
            catch (Exception ex)
            {
                AddLog($"插入重叠区域要素失败: {ex.Message}");
                throw;
            }
        }
    }
}
