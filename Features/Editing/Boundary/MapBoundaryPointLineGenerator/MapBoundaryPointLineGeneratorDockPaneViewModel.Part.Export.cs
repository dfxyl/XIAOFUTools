using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Editing.Boundary.MapBoundaryPointLineGenerator
{
    internal partial class MapBoundaryPointLineGeneratorDockPaneViewModel
    {
        /// <summary>
        /// 一次性创建所有模板
        /// </summary>
        private async void CreateAllTemplates()
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var layout = GetSelectedLayout();
                    if (layout == null)
                    {
                        PresentationServices.UiThread.InvokeOrRun(() =>
                            PresentationServices.Dialogs.Show("请先选择布局", "提示"));
                        return;
                    }

                    var createdList = new List<string>();
                    var existsList = new List<string>();
                    var redColor = CIMColor.CreateRGBColor(255, 0, 0);

                    // XF_JZD - 界址点（实心圆无边框）
                    if (layout.FindElement("XF_JZD") == null)
                    {
                        var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(redColor, 6, SimpleMarkerStyle.Circle);
                        // 移除边框
                        if (pointSymbol.SymbolLayers != null)
                        {
                            foreach (var layer in pointSymbol.SymbolLayers.OfType<CIMVectorMarker>())
                            {
                                if (layer.MarkerGraphics != null)
                                {
                                    foreach (var mg in layer.MarkerGraphics)
                                    {
                                        if (mg.Symbol is CIMPolygonSymbol polySymbol)
                                        {
                                            polySymbol.SymbolLayers = polySymbol.SymbolLayers?
                                                .Where(sl => sl is CIMSolidFill).ToArray();
                                        }
                                    }
                                }
                            }
                        }
                        var pointGraphic = new CIMPointGraphic
                        {
                            Location = MapPointBuilderEx.CreateMapPoint(-35, -35),
                            Symbol = pointSymbol.MakeSymbolReference()
                        };
                        ElementFactory.Instance.CreateGraphicElement(layout, pointGraphic, "XF_JZD", true, new ElementInfo());
                        createdList.Add("XF_JZD(界址点)");
                    }
                    else existsList.Add("XF_JZD");

                    // XF_JZX - 界址线
                    if (layout.FindElement("XF_JZX") == null)
                    {
                        var lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(redColor, 1.0, SimpleLineStyle.Solid);
                        var polyline = PolylineBuilderEx.CreatePolyline(new[] {
                            MapPointBuilderEx.CreateMapPoint(-45, -35),
                            MapPointBuilderEx.CreateMapPoint(-25, -35)
                        });
                        var lineGraphic = new CIMLineGraphic { Line = polyline, Symbol = lineSymbol.MakeSymbolReference() };
                        ElementFactory.Instance.CreateGraphicElement(layout, lineGraphic, "XF_JZX", true, new ElementInfo());
                        createdList.Add("XF_JZX(界址线)");
                    }
                    else existsList.Add("XF_JZX");

                    // XF_DH - 点号（宋体）
                    if (layout.FindElement("XF_DH") == null)
                    {
                        var textSymbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, 12, "宋体", "Regular");
                        var textGraphic = new CIMTextGraphic
                        {
                            Shape = MapPointBuilderEx.CreateMapPoint(-35, -40),
                            Text = "J1",
                            Symbol = textSymbol.MakeSymbolReference()
                        };
                        ElementFactory.Instance.CreateGraphicElement(layout, textGraphic, "XF_DH", true, new ElementInfo());
                        createdList.Add("XF_DH(点号)");
                    }
                    else existsList.Add("XF_DH");

                    // XF_BC - 边长（宋体）
                    if (layout.FindElement("XF_BC") == null)
                    {
                        var textSymbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, 10, "宋体", "Regular");
                        var textGraphic = new CIMTextGraphic
                        {
                            Shape = MapPointBuilderEx.CreateMapPoint(-35, -50),
                            Text = "12.34",
                            Symbol = textSymbol.MakeSymbolReference()
                        };
                        ElementFactory.Instance.CreateGraphicElement(layout, textGraphic, "XF_BC", true, new ElementInfo());
                        createdList.Add("XF_BC(边长)");
                    }
                    else existsList.Add("XF_BC");

                    // 汇总提示
                    var msg = new StringBuilder();
                    if (createdList.Count > 0)
                        msg.AppendLine($"已创建: {string.Join(", ", createdList)}");
                    if (existsList.Count > 0)
                        msg.AppendLine($"已存在: {string.Join(", ", existsList)}");
                    msg.AppendLine("\n模板位于版面外（左下角负坐标），可调整样式。");

                    PresentationServices.UiThread.InvokeOrRun(() =>
                        PresentationServices.Dialogs.Show(msg.ToString(), createdList.Count > 0 ? "已创建模板" : "提示"));
                });
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show($"创建模板失败: {ex.Message}", "错误");
            }
        }
        private GraphicsLayer GetOrCreateGraphicsLayer(Map map, string layerName)
        {
            try
            {
                var existingLayer = map.GetLayersAsFlattenedList()
                    .OfType<GraphicsLayer>()
                    .FirstOrDefault(l => l.Name == layerName);

                if (existingLayer != null)
                    return existingLayer;

                var graphicsLayerParams = new GraphicsLayerCreationParams { Name = layerName };
                return LayerFactory.Instance.CreateLayer<GraphicsLayer>(graphicsLayerParams, map);
            }
            catch (Exception ex)
            {
                LogError($"创建图形图层失败: {ex.Message}");
                return null;
            }
        }

        private void CreateTextGraphic(GraphicsLayer layer, MapPoint position, string text, CIMTextSymbol symbol)
        {
            var textGraphic = new CIMTextGraphic
            {
                Shape = position,
                Text = text,
                Symbol = symbol.MakeSymbolReference()
            };
            layer.AddElement(textGraphic);
        }

        private void CreateRotatedTextGraphic(GraphicsLayer layer, MapPoint position, string text, CIMTextSymbol symbol, double angleDegrees)
        {
            var rotatedSymbol = symbol.Clone() as CIMTextSymbol;
            if (rotatedSymbol != null)
            {
                rotatedSymbol.Angle = angleDegrees;
            }

            var textGraphic = new CIMTextGraphic
            {
                Shape = position,
                Text = text,
                Symbol = (rotatedSymbol ?? symbol).MakeSymbolReference()
            };
            layer.AddElement(textGraphic);
        }
    }
}
