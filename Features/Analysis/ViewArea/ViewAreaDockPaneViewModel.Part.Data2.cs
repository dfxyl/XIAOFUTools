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
        /// 计算椭球面积（使用ArcGIS Pro内置高精度方法）
        /// </summary>
        private double CalculateGeodesicArea(Polygon polygon)
        {
            try
            {
                // 使用ArcGIS Pro内置的高精度椭球面积计算
                // 这个方法内部已经处理了大地测量的复杂计算
                var geodesicArea = GeometryEngine.Instance.GeodesicArea(polygon);

                System.Diagnostics.Debug.WriteLine($"椭球面积计算 - 原始结果: {geodesicArea}");

                // 检查结果是否合理
                if (double.IsNaN(geodesicArea) || double.IsInfinity(geodesicArea) || geodesicArea < 0)
                {
                    System.Diagnostics.Debug.WriteLine("椭球面积计算结果异常，尝试备用方法");

                    // 备用方法：使用平面面积作为参考
                    var planarArea = GeometryEngine.Instance.Area(polygon);
                    System.Diagnostics.Debug.WriteLine($"备用平面面积: {planarArea}");

                    // 如果平面面积合理，返回平面面积
                    if (!double.IsNaN(planarArea) && !double.IsInfinity(planarArea) && planarArea > 0)
                    {
                        return planarArea;
                    }

                    return 0;
                }

                return Math.Abs(geodesicArea); // 确保返回正值
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"椭球面积计算失败: {ex.Message}");

                try
                {
                    // 备用方法：使用平面面积
                    var planarArea = GeometryEngine.Instance.Area(polygon);
                    System.Diagnostics.Debug.WriteLine($"使用备用平面面积: {planarArea}");
                    return Math.Abs(planarArea);
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"备用面积计算也失败: {ex2.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// 计算测地线长度（使用ArcGIS Pro内置高精度方法）
        /// </summary>
        private double CalculateGeodesicLength(Polyline polyline)
        {
            try
            {
                // 使用ArcGIS Pro内置的高精度测地线长度计算
                var geodesicLength = GeometryEngine.Instance.GeodesicLength(polyline);

                System.Diagnostics.Debug.WriteLine($"测地线长度计算 - 原始结果: {geodesicLength}");

                // 检查结果是否合理
                if (double.IsNaN(geodesicLength) || double.IsInfinity(geodesicLength) || geodesicLength < 0)
                {
                    System.Diagnostics.Debug.WriteLine("测地线长度计算结果异常，尝试备用方法");

                    // 备用方法：使用平面长度作为参考
                    var planarLength = GeometryEngine.Instance.Length(polyline);
                    System.Diagnostics.Debug.WriteLine($"备用平面长度: {planarLength}");

                    // 如果平面长度合理，返回平面长度
                    if (!double.IsNaN(planarLength) && !double.IsInfinity(planarLength) && planarLength > 0)
                    {
                        return planarLength;
                    }

                    return 0;
                }

                return Math.Abs(geodesicLength); // 确保返回正值
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"测地线长度计算失败: {ex.Message}");

                try
                {
                    // 备用方法：使用平面长度
                    var planarLength = GeometryEngine.Instance.Length(polyline);
                    System.Diagnostics.Debug.WriteLine($"使用备用平面长度: {planarLength}");
                    return Math.Abs(planarLength);
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"备用长度计算也失败: {ex2.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// 递归获取所有要素图层（包括组图层中的图层）
        /// </summary>
        private IEnumerable<FeatureLayer> GetAllFeatureLayers(IList<Layer> layers)
        {
            foreach (var layer in layers)
            {
                if (layer is GroupLayer groupLayer)
                {
                    // 递归遍历组图层
                    foreach (var childLayer in GetAllFeatureLayers(groupLayer.Layers))
                    {
                        yield return childLayer;
                    }
                }
                else if (layer is FeatureLayer featureLayer)
                {
                    yield return featureLayer;
                }
            }
        }

        /// <summary>
        /// 获取图层的坐标系
        /// </summary>
        private SpatialReference GetLayerSpatialReference(string layerName)
        {
            try
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null) return null;

                var layer = GetAllFeatureLayers(mapView.Map.Layers).FirstOrDefault(l => l.Name == layerName);
                return layer?.GetSpatialReference();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取图层坐标系时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取坐标系类型
        /// </summary>
        private CoordinateSystemType GetCoordinateSystemType(SpatialReference spatialRef)
        {
            try
            {
                if (spatialRef == null)
                {
                    System.Diagnostics.Debug.WriteLine("坐标系为null");
                    return CoordinateSystemType.None;
                }

                if (string.IsNullOrEmpty(spatialRef.Name))
                {
                    System.Diagnostics.Debug.WriteLine("坐标系名称为空");
                    return CoordinateSystemType.None;
                }

                System.Diagnostics.Debug.WriteLine($"坐标系名称: {spatialRef.Name}");
                System.Diagnostics.Debug.WriteLine($"IsGeographic: {spatialRef.IsGeographic}");
                System.Diagnostics.Debug.WriteLine($"IsProjected: {spatialRef.IsProjected}");

                if (spatialRef.IsGeographic)
                    return CoordinateSystemType.Geographic;

                if (spatialRef.IsProjected)
                    return CoordinateSystemType.Projected;

                return CoordinateSystemType.None;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检测坐标系类型时出错: {ex.Message}");
                return CoordinateSystemType.None;
            }
        }

        /// <summary>
        /// 获取坐标系类型描述
        /// </summary>
        private string GetCoordinateSystemTypeDescription(CoordinateSystemType coordinateSystemType)
        {
            return coordinateSystemType switch
            {
                CoordinateSystemType.None => "无坐标系",
                CoordinateSystemType.Geographic => "地理坐标系",
                CoordinateSystemType.Projected => "投影坐标系",
                _ => "未知坐标系"
            };
        }
    }
}
