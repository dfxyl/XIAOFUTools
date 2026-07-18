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
        /// 获取或创建图形图层
        /// </summary>
        private GraphicsLayer GetOrCreateGraphicsLayer(Map map, string layerName)
        {
            try
            {
                // 查找现有图形图层
                var existingLayer = map.GetLayersAsFlattenedList()
                    .OfType<GraphicsLayer>()
                    .FirstOrDefault(l => l.Name == layerName);
                
                if (existingLayer != null)
                {
                    return existingLayer;
                }
                
                // 创建新的图形图层
                var graphicsLayerParams = new GraphicsLayerCreationParams
                {
                    Name = layerName
                };
                
                var newLayer = LayerFactory.Instance.CreateLayer<GraphicsLayer>(graphicsLayerParams, map);
                return newLayer;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取或创建图形图层失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 创建红色点符号
        /// </summary>
        private CIMPointSymbol CreateRedPointSymbol(double sizeInPoints)
        {
            // 使用SymbolFactory创建简单圆形标记符号
            // 红色填充，深红色边框
            var redColor = CIMColor.CreateRGBColor(255, 0, 0);
            var darkRedColor = CIMColor.CreateRGBColor(139, 0, 0);
            
            // 创建简单标记符号（圆形）
            var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(
                redColor,
                sizeInPoints,
                SimpleMarkerStyle.Circle);
            
            // 设置边框
            if (pointSymbol.SymbolLayers != null && pointSymbol.SymbolLayers.Length > 0)
            {
                var markerLayer = pointSymbol.SymbolLayers[0] as CIMVectorMarker;
                if (markerLayer?.MarkerGraphics != null && markerLayer.MarkerGraphics.Length > 0)
                {
                    var markerGraphic = markerLayer.MarkerGraphics[0];
                    if (markerGraphic.Symbol is CIMPolygonSymbol polySymbol)
                    {
                        // 添加深红色边框
                        var strokeSymbol = SymbolFactory.Instance.ConstructStroke(darkRedColor, 0.5, SimpleLineStyle.Solid);
                        var solidFill = SymbolFactory.Instance.ConstructSolidFill(redColor);
                        polySymbol.SymbolLayers = new CIMSymbolLayer[] { strokeSymbol, solidFill };
                    }
                }
            }
            
            return pointSymbol;
        }
    }
}
