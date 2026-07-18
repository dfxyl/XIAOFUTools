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
        
        /// <summary>
        /// 获取边长标注候选位置列表（边的外侧）
        /// </summary>
        private List<MapPoint> GetEdgeLabelCandidatePositions(MapPoint p1, MapPoint p2, double distance, List<MapPoint> polygon, SpatialReference sr)
        {
            var candidates = new List<MapPoint>();
            
            // 边的中点
            double midX = (p1.X + p2.X) / 2;
            double midY = (p1.Y + p2.Y) / 2;
            
            // 边的方向向量
            double edgeX = p2.X - p1.X;
            double edgeY = p2.Y - p1.Y;
            double edgeLen = Math.Sqrt(edgeX * edgeX + edgeY * edgeY);
            if (edgeLen < 0.0001) edgeLen = 0.0001;
            edgeX /= edgeLen;
            edgeY /= edgeLen;
            
            // 边的法向量（左侧和右侧）
            double n1x = -edgeY, n1y = edgeX;  // 左侧法向
            double n2x = edgeY, n2y = -edgeX;   // 右侧法向
            
            // 测试哪个方向是外侧
            double test1X = midX + n1x * distance * 0.1;
            double test1Y = midY + n1y * distance * 0.1;
            bool n1IsOutside = !IsPointInPolygon(test1X, test1Y, polygon);
            
            // 优先使用外侧方向
            double primaryNx = n1IsOutside ? n1x : n2x;
            double primaryNy = n1IsOutside ? n1y : n2y;
            double secondaryNx = n1IsOutside ? n2x : n1x;
            double secondaryNy = n1IsOutside ? n2y : n1y;
            
            // 不同距离的候选位置
            double[] distanceFactors = { 1.0, 1.5, 2.0, 2.5 };
            
            // 外侧候选位置
            foreach (var factor in distanceFactors)
            {
                double d = distance * factor;
                double posX = midX + primaryNx * d;
                double posY = midY + primaryNy * d;
                candidates.Add(MapPointBuilderEx.CreateMapPoint(posX, posY, sr));
            }
            
            // 内侧候选位置（作为备选）
            foreach (var factor in distanceFactors)
            {
                double d = distance * factor;
                double posX = midX + secondaryNx * d;
                double posY = midY + secondaryNy * d;
                if (!IsPointInPolygon(posX, posY, polygon))
                {
                    candidates.Add(MapPointBuilderEx.CreateMapPoint(posX, posY, sr));
                }
            }
            
            // 沿边方向偏移的候选位置
            double[] offsetFactors = { 0.2, -0.2, 0.3, -0.3 };
            foreach (var offsetFactor in offsetFactors)
            {
                double offsetX = midX + edgeX * edgeLen * offsetFactor;
                double offsetY = midY + edgeY * edgeLen * offsetFactor;
                double posX = offsetX + primaryNx * distance;
                double posY = offsetY + primaryNy * distance;
                if (!IsPointInPolygon(posX, posY, polygon))
                {
                    candidates.Add(MapPointBuilderEx.CreateMapPoint(posX, posY, sr));
                }
            }
            
            return candidates;
        }
        
        /// <summary>
        /// 获取候选标注位置列表（兼容旧版本）
        /// </summary>
        private List<MapPoint> GetCandidateLabelPositions(List<MapPoint> points, int index, double distance, SpatialReference sr)
        {
            return GetCandidateLabelPositions8Dir(points, index, distance, sr);
        }
    }
}
