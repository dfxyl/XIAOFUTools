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

        // 获取当前选中的 Layout 对象
        private Layout GetSelectedLayout()
        {
            if (string.IsNullOrEmpty(SelectedLayoutName)) return null;
            var items = Project.Current?.GetItems<LayoutProjectItem>();
            var layoutItem = items?.FirstOrDefault(i => i.Name == SelectedLayoutName);
            return layoutItem?.GetLayout();
        }
        private void LoadPolygonLayers()
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var list = await LayerUtils.GetPolygonLayersAsync();
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        PolygonLayers.Clear();
                        foreach (var fl in list) PolygonLayers.Add(fl);
                        if (PolygonLayers.Count > 0) SelectedPolygonLayer = PolygonLayers[0];
                    });
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                        StatusMessage = $"加载图层失败: {ex.Message}");
                }
            });
        }

        private void LoadLayouts()
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var layoutNames = await QueuedTask.Run(() =>
                    {
                        var items = Project.Current?.GetItems<LayoutProjectItem>();
                        return items?.Select(i => i.Name).ToList() ?? new List<string>();
                    });

                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        LayoutNames.Clear();
                        foreach (var name in layoutNames) LayoutNames.Add(name);
                        if (LayoutNames.Count > 0) SelectedLayoutName = LayoutNames[0];
                    });
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                        StatusMessage = $"加载布局失败: {ex.Message}");
                }
            });
        }

        private void LoadAvailableFields()
        {
            AvailableFields.Clear();
            if (SelectedPolygonLayer == null) return;

            QueuedTask.Run(() =>
            {
                try
                {
                    using var table = SelectedPolygonLayer.GetTable();
                    var fields = table?.GetDefinition()?.GetFields();
                    if (fields == null) return;

                    var textFields = fields.Where(f => f.FieldType == FieldType.String || f.FieldType == FieldType.Integer || f.FieldType == FieldType.SmallInteger)
                        .Select(f => f.Name).ToList();

                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        foreach (var n in textFields) AvailableFields.Add(n);
                        SelectedUniqueField = AvailableFields.FirstOrDefault(n =>
                            n.Equals("ZDDM", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("DM", StringComparison.OrdinalIgnoreCase) ||
                            n.Equals("FID", StringComparison.OrdinalIgnoreCase) ||
                            n.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase)) ?? AvailableFields.FirstOrDefault();
                    });
                }
                catch (Exception ex)
                {
                    LogError($"读取字段失败: {ex.Message}");
                }
            });
        }

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
            catch { }

            if (symbol == null)
            {
                var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                symbol = SymbolFactory.Instance.ConstructPointSymbol(redColor, userSize, SimpleMarkerStyle.Circle);
            }
            else
            {
                symbol = symbol.Clone() as CIMPointSymbol;
                symbol?.SetSize(userSize);
            }
            return symbol;
        }

        private CIMLineSymbol GetLineSymbolFromTemplate(Layout layout, double userWidth)
        {
            CIMLineSymbol symbol = null;
            try
            {
                var template = layout.FindElement("XF_JZX") as GraphicElement;
                if (template != null)
                {
                    var graphic = template.GetGraphic();
                    if (graphic is CIMLineGraphic lineGraphic)
                    {
                        symbol = lineGraphic.Symbol?.Symbol as CIMLineSymbol;
                    }
                }
            }
            catch { }

            if (symbol == null)
            {
                var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                symbol = SymbolFactory.Instance.ConstructLineSymbol(redColor, userWidth, SimpleLineStyle.Solid);
            }
            else
            {
                symbol = symbol.Clone() as CIMLineSymbol;
                symbol?.SetSize(userWidth);
            }
            return symbol;
        }

        private CIMTextSymbol GetTextSymbolFromTemplate(Layout layout, double userSize, string templateName)
        {
            CIMTextSymbol symbol = null;
            try
            {
                var template = layout.FindElement(templateName) as GraphicElement;
                if (template != null)
                {
                    var graphic = template.GetGraphic();
                    if (graphic is CIMTextGraphic textGraphic)
                    {
                        symbol = textGraphic.Symbol?.Symbol as CIMTextSymbol;
                    }
                }
            }
            catch { }

            if (symbol == null)
            {
                var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                symbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, userSize, "Arial", "Regular");
            }
            else
            {
                symbol = symbol.Clone() as CIMTextSymbol;
                symbol?.SetSize(userSize);
            }

            symbol.HorizontalAlignment = HorizontalAlignment.Center;
            symbol.VerticalAlignment = VerticalAlignment.Center;
            return symbol;
        }

        private string GetStringSafe(Feature feature, string fieldName)
        {
            if (feature == null || string.IsNullOrEmpty(fieldName)) return null;
            try
            {
                var value = feature[fieldName];
                return value?.ToString();
            }
            catch { return null; }
        }

        private int GetFeatureCount(FeatureLayer layer)
        {
            try
            {
                using var table = layer.GetTable();
                using var cursor = table.Search();
                int count = 0;
                while (cursor.MoveNext()) count++;
                return count;
            }
            catch { return 0; }
        }
    }
}
