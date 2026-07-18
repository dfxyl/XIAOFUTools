using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    public partial class MapSeriesExportViewModel
    {

        private (double X, double Y) ResolveTableStartPosition(
            Layout layout,
            CoordinateTableSettings settings,
            string placementCorner,
            double cornerOffset,
            double tableWidth,
            double tableHeight)
        {
            if (settings.UseAnchorPosition)
            {
                var anchorElement = layout.FindElement(settings.AnchorElementName) as GraphicElement;
                if (anchorElement != null)
                {
                    var anchorBounds = anchorElement.GetBounds();
                    double anchorX = (anchorBounds.XMin + anchorBounds.XMax) / 2;
                    double anchorY = (anchorBounds.YMin + anchorBounds.YMax) / 2;
                    return ResolveCornerPosition(anchorX, anchorY, anchorX, anchorY, placementCorner, cornerOffset, tableWidth, tableHeight);
                }

                return (10, 300);
            }

            var mapFrame = ResolveMapFrame(layout, settings);
            if (mapFrame != null)
            {
                var mapBounds = mapFrame.GetBounds();
                return ResolveCornerPosition(
                    mapBounds.XMin,
                    mapBounds.YMin,
                    mapBounds.XMax,
                    mapBounds.YMax,
                    placementCorner,
                    cornerOffset,
                    tableWidth,
                    tableHeight);
            }

            return (10, 300);
        }


        private static (double X, double Y) ResolveCornerPosition(
            double xMin,
            double yMin,
            double xMax,
            double yMax,
            string placementCorner,
            double cornerOffset,
            double tableWidth,
            double tableHeight)
        {
            return placementCorner switch
            {
                "右下角" => (xMax - tableWidth - cornerOffset, yMin + cornerOffset + tableHeight),
                "左上角" => (xMin + cornerOffset, yMax - cornerOffset),
                "右上角" => (xMax - tableWidth - cornerOffset, yMax - cornerOffset),
                _ => (xMin + cornerOffset, yMin + cornerOffset + tableHeight)
            };
        }


        private MapFrame ResolveMapFrame(Layout layout, CoordinateTableSettings settings)
        {
            MapFrame mapFrame = null;
            if (!string.IsNullOrEmpty(settings?.MapFrameName))
            {
                mapFrame = layout.FindElement(settings.MapFrameName) as MapFrame;
            }

            return mapFrame ?? layout.Elements.OfType<MapFrame>().FirstOrDefault();
        }

        
        /// <summary>
        /// 从布局模板获取点符号（应用用户设置的大小）
        /// </summary>
        private CIMPointSymbol GetPointSymbolFromTemplate(Layout layout, double userSize)
        {
            CIMPointSymbol symbol = null;
            
            try
            {
                var template = layout.FindElement("XF_JZD") as GraphicElement;
                if (template != null)
                {
                    var graphic = template.GetGraphic();
                    if (graphic is CIMPointGraphic pointGraphic)
                    {
                        symbol = pointGraphic.Symbol?.Symbol as CIMPointSymbol;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取点符号模板失败: {ex.Message}");
            }
            
            // 如果没有模板，创建默认红色点符号
            if (symbol == null)
            {
                symbol = CreateRedPointSymbol(userSize);
            }
            else
            {
                // 有模板时，克隆并应用用户设置的大小
                symbol = symbol.Clone() as CIMPointSymbol;
                if (symbol != null)
                {
                    symbol.SetSize(userSize);
                }
            }
            
            return symbol;
        }
        
        /// <summary>
        /// 从布局模板获取文本符号（应用用户设置的大小，设置为中心对齐）
        /// </summary>
        private CIMTextSymbol GetTextSymbolFromTemplate(Layout layout, double userSize)
        {
            CIMTextSymbol symbol = null;
            
            try
            {
                var template = layout.FindElement("XF_DH") as GraphicElement;
                if (template != null)
                {
                    var graphic = template.GetGraphic();
                    if (graphic is CIMTextGraphic textGraphic)
                    {
                        symbol = textGraphic.Symbol?.Symbol as CIMTextSymbol;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取文本符号模板失败: {ex.Message}");
            }
            
            // 如果没有模板，创建默认红色文本符号
            if (symbol == null)
            {
                var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                symbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, userSize, "Arial", "Regular");
            }
            else
            {
                // 有模板时，克隆并应用用户设置的大小
                symbol = symbol.Clone() as CIMTextSymbol;
                if (symbol != null)
                {
                    symbol.SetSize(userSize);
                }
            }
            
            // 设置文本为中心对齐（水平和垂直居中）
            symbol.HorizontalAlignment = ArcGIS.Core.CIM.HorizontalAlignment.Center;
            symbol.VerticalAlignment = ArcGIS.Core.CIM.VerticalAlignment.Center;
            
            return symbol;
        }
        
        /// <summary>
        /// 从布局模板获取边长文本符号（应用用户设置的大小）
        /// </summary>
        private CIMTextSymbol GetEdgeTextSymbolFromTemplate(Layout layout, double userSize)
        {
            CIMTextSymbol symbol = null;
            
            try
            {
                var template = layout.FindElement("XF_BC") as GraphicElement;
                if (template != null)
                {
                    var graphic = template.GetGraphic();
                    if (graphic is CIMTextGraphic textGraphic)
                    {
                        symbol = textGraphic.Symbol?.Symbol as CIMTextSymbol;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取边长文本符号模板失败: {ex.Message}");
            }
            
            // 如果没有模板，创建默认红色文本符号
            if (symbol == null)
            {
                var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                symbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, userSize, "Arial", "Regular");
            }
            else
            {
                // 有模板时，克隆并应用用户设置的大小
                symbol = symbol.Clone() as CIMTextSymbol;
                if (symbol != null)
                {
                    symbol.SetSize(userSize);
                }
            }
            
            // 设置文本为中心对齐
            symbol.HorizontalAlignment = ArcGIS.Core.CIM.HorizontalAlignment.Center;
            symbol.VerticalAlignment = ArcGIS.Core.CIM.VerticalAlignment.Center;
            
            return symbol;
        }
        
        /// <summary>
        /// 计算点号标注位置（始终在多边形外部，沿外角角平分线方向）
        /// </summary>
        private MapPoint CalculateLabelPosition(List<MapPoint> points, int index, double distance, SpatialReference sr)
        {
            int count = points.Count;
            var current = points[index];
            var prev = points[(index - 1 + count) % count];
            var next = points[(index + 1) % count];
            
            // 计算从当前点指向前后点的向量
            double v1x = prev.X - current.X;
            double v1y = prev.Y - current.Y;
            double v2x = next.X - current.X;
            double v2y = next.Y - current.Y;
            
            // 归一化
            double len1 = Math.Sqrt(v1x * v1x + v1y * v1y);
            double len2 = Math.Sqrt(v2x * v2x + v2y * v2y);
            if (len1 > 0.0001) { v1x /= len1; v1y /= len1; }
            if (len2 > 0.0001) { v2x /= len2; v2y /= len2; }
            
            // 角平分线方向 = 两个单位向量之和
            double bisectX = v1x + v2x;
            double bisectY = v1y + v2y;
            double bisectLen = Math.Sqrt(bisectX * bisectX + bisectY * bisectY);
            
            if (bisectLen < 0.0001)
            {
                // 180度角，使用v1的垂直方向
                bisectX = -v1y;
                bisectY = v1x;
                bisectLen = 1.0;
            }
            else
            {
                bisectX /= bisectLen;
                bisectY /= bisectLen;
            }
            
            // 测试bisect方向的点是否在多边形内部
            double testDist = distance * 0.1; // 用小距离测试
            double testX = current.X + bisectX * testDist;
            double testY = current.Y + bisectY * testDist;
            
            bool isInside = IsPointInPolygon(testX, testY, points);
            
            // 如果测试点在内部，说明bisect指向内部，需要取反方向
            // 如果测试点在外部，说明bisect指向外部，保持方向
            double outX, outY;
            if (isInside)
            {
                // bisect指向内部，取反得到外部方向
                outX = -bisectX;
                outY = -bisectY;
            }
            else
            {
                // bisect已经指向外部
                outX = bisectX;
                outY = bisectY;
            }
            
            // 计算标注位置（沿外部方向偏移）
            double labelX = current.X + outX * distance;
            double labelY = current.Y + outY * distance;
            
            return MapPointBuilderEx.CreateMapPoint(labelX, labelY, sr);
        }
        
        /// <summary>
        /// 获变8方向候选标注位置列表（类CASS的选位算法）
        /// 优先级：角平分线外侧 > 右上 > 左上 > 右下 > 左下 > 右 > 上 > 左 > 下
        /// </summary>
        private List<MapPoint> GetCandidateLabelPositions8Dir(List<MapPoint> points, int index, double distance, SpatialReference sr)
        {
            var candidates = new List<MapPoint>();
            int count = points.Count;
            var current = points[index];
            
            // 首选位置：角平分线外侧
            var primaryPos = CalculateLabelPosition(points, index, distance, sr);
            candidates.Add(primaryPos);
            
            // 8方向候选位置（类CASS）
            double sqrt2 = Math.Sqrt(2) / 2;
            var directions = new (double dx, double dy, string name)[]
            {
                (sqrt2, sqrt2, "右上"),      // 右上
                (-sqrt2, sqrt2, "左上"),     // 左上
                (sqrt2, -sqrt2, "右下"),     // 右下
                (-sqrt2, -sqrt2, "左下"),    // 左下
                (1, 0, "右"),                // 右
                (0, 1, "上"),                // 上
                (-1, 0, "左"),               // 左
                (0, -1, "下")                // 下
            };
            
            // 不同距离的候选位置
            double[] distanceFactors = { 1.0, 1.3, 1.6, 2.0 };
            
            foreach (var factor in distanceFactors)
            {
                double d = distance * factor;
                
                foreach (var (dx, dy, name) in directions)
                {
                    double posX = current.X + dx * d;
                    double posY = current.Y + dy * d;
                    
                    // 只添加在多边形外部的位置
                    if (!IsPointInPolygon(posX, posY, points))
                    {
                        candidates.Add(MapPointBuilderEx.CreateMapPoint(posX, posY, sr));
                    }
                }
            }
            
            // 远距离候选位置（角平分线方向）
            candidates.Add(CalculateLabelPosition(points, index, distance * 2.5, sr));
            candidates.Add(CalculateLabelPosition(points, index, distance * 3.0, sr));
            
            return candidates;
        }
    }
}
