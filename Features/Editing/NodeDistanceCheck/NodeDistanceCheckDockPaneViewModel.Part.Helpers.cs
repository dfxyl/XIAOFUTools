using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using ArcGIS.Desktop.Editing;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.NodeDistanceCheck
{
    internal partial class NodeDistanceCheckDockPaneViewModel
    {

        /// <summary>
        /// 检查节点距离
        /// </summary>
        private List<(Polyline line, Dictionary<string, object> attributes)> CheckNodeDistances(Polygon polygon, long featureOID, Feature feature)
        {
            var resultLines = new List<(Polyline line, Dictionary<string, object> attributes)>();

            try
            {
                // 获取多边形的所有顶点
                var points = new List<MapPoint>();
                foreach (var part in polygon.Parts)
                {
                    foreach (var segment in part)
                    {
                        points.Add(segment.StartPoint);
                    }
                    // 添加最后一个点
                    if (part.Count > 0)
                    {
                        points.Add(part[part.Count - 1].EndPoint);
                    }
                }

                // 检查相邻节点之间的距离
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var point1 = points[i];
                    var point2 = points[i + 1];
                    
                    double distance = GeometryEngine.Instance.Distance(point1, point2);
                    
                    bool meetsCriteria = false;
                    switch (SelectedCheckOption)
                    {
                        case "小于等于":
                            meetsCriteria = distance <= CheckDistance;
                            break;
                        case "小于":
                            meetsCriteria = distance < CheckDistance;
                            break;
                        case "大于等于":
                            meetsCriteria = distance >= CheckDistance;
                            break;
                        case "大于":
                            meetsCriteria = distance > CheckDistance;
                            break;
                        case "等于":
                            meetsCriteria = Math.Abs(distance - CheckDistance) < 0.001; // 允许小的浮点误差
                            break;
                    }

                    if (meetsCriteria)
                    {
                        // 创建连接两个节点的线
                        var linePoints = new List<MapPoint> { point1, point2 };
                        var line = PolylineBuilderEx.CreatePolyline(linePoints, polygon.SpatialReference);

                        var attributes = new Dictionary<string, object>
                        {
                            ["源要素ID"] = featureOID,
                            ["节点距离"] = Math.Round(distance, 3),
                            ["检查条件"] = $"{SelectedCheckOption} {CheckDistance}m"
                        };

                        // 添加保留字段的值
                        if (SelectedFields != null && SelectedFields.Count > 0)
                        {
                            foreach (var fieldName in SelectedFields)
                            {
                                try
                                {
                                    var fieldValue = feature[fieldName];
                                    attributes[fieldName] = fieldValue;
                                }
                                catch (Exception ex)
                                {
                                    LogWarning($"获取字段 {fieldName} 的值失败: {ex.Message}");
                                    attributes[fieldName] = null;
                                }
                            }
                        }

                        resultLines.Add((line, attributes));
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"检查要素 {featureOID} 的节点距离时发生错误: {ex.Message}");
            }

            return resultLines;
        }

        /// <summary>
        /// 插入线要素
        /// </summary>
        private async Task InsertLineFeatures(string outputFeatureClassPath, List<(Polyline line, Dictionary<string, object> attributes)> linesToCreate)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var workspace = Path.GetDirectoryName(outputFeatureClassPath);
                    var featureClassName = Path.GetFileNameWithoutExtension(outputFeatureClassPath);

                    if (string.IsNullOrEmpty(workspace) || string.IsNullOrEmpty(featureClassName))
                    {
                        LogError("无效的输出要素类路径");
                        return;
                    }

                    using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace))))
                    using (var featureClass = geodatabase.OpenDataset<FeatureClass>(featureClassName))
                    {
                        int insertedCount = 0;
                        
                        foreach (var (line, attributes) in linesToCreate)
                        {
                            if (CancelRequested) break;

                            try
                            {
                                using (var rowBuffer = featureClass.CreateRowBuffer())
                                {
                                    // 设置几何
                                    rowBuffer[featureClass.GetDefinition().GetShapeField()] = line;

                                    // 设置属性
                                    foreach (var attr in attributes)
                                    {
                                        try
                                        {
                                            rowBuffer[attr.Key] = attr.Value;
                                        }
                                        catch (Exception ex)
                                        {
                                            LogWarning($"设置字段 {attr.Key} 的值失败: {ex.Message}");
                                        }
                                    }

                                    using (var feature = featureClass.CreateRow(rowBuffer))
                                    {
                                        feature.Store();
                                        insertedCount++;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError($"插入要素失败: {ex.Message}");
                            }
                        }

                        LogInfo($"成功插入 {insertedCount} 条线要素");
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"插入线要素时发生错误: {ex.Message}");
            }
        }
    }
}
