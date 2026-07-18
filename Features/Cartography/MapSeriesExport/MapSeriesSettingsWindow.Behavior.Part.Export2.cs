#define DEBUG
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    public partial class MapSeriesSettingsWindow
    {

    private async void CreateEdgeLabelTemplate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await QueuedTask.Run((Action)delegate
            {
                //IL_00cb: Unknown result type (might be due to invalid IL or missing references)
                //IL_00d0: Unknown result type (might be due to invalid IL or missing references)
                //IL_00d9: Unknown result type (might be due to invalid IL or missing references)
                //IL_00e5: Unknown result type (might be due to invalid IL or missing references)
                //IL_00f5: Expected O, but got Unknown
                //IL_00f5: Unknown result type (might be due to invalid IL or missing references)
                //IL_00fa: Unknown result type (might be due to invalid IL or missing references)
                //IL_0108: Expected O, but got Unknown
                LayoutView active = LayoutView.Active;
                if (active == null)
                {
                    ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("请先打开布局视图", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation)));
                }
                else
                {
                    Layout layout = active.Layout;
                    Element val = layout.FindElement("XF_BC");
                    if (val == null)
                    {
                        CIMColor val2 = CIMColor.CreateRGBColor(255.0, 0.0, 0.0, 100.0);
                        CIMTextSymbol val3 = SymbolFactory.Instance.ConstructTextSymbol(val2, 10.0, "Arial", "Regular");
                        MapPoint shape = MapPointBuilderEx.CreateMapPoint(-35.0, -50.0, (SpatialReference)null);
                        CIMTextGraphic val4 = new CIMTextGraphic
                        {
                            Shape = (Geometry)(object)shape,
                            Text = "12.34",
                            Symbol = SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)val3)
                        };
                        ElementInfo val5 = new ElementInfo
                        {
                            CustomProperties = new List<CIMStringMap>()
                        };
                        GraphicElement val6 = ElementFactory.Instance.CreateGraphicElement((IElementContainer)(object)layout, (CIMGraphic)(object)val4, "XF_BC", true, val5);
                        ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("已创建边长文本模板（XF_BC）\n位于版面外（左下角负坐标），可设置字体、颜色等样式。", "成功", MessageBoxButton.OK, MessageBoxImage.Asterisk)));
                    }
                    else
                    {
                        ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("边长文本模板已存在（XF_BC），无需重复创建。\n可直接修改模板样式。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk)));
                    }
                }
            }, TaskCreationOptions.None);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            MessageBox.Show("创建边长模板失败: " + ex2.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
    }

    private async void CreateLabelTemplate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await QueuedTask.Run((Action)delegate
            {
                //IL_00cb: Unknown result type (might be due to invalid IL or missing references)
                //IL_00d0: Unknown result type (might be due to invalid IL or missing references)
                //IL_00d9: Unknown result type (might be due to invalid IL or missing references)
                //IL_00e5: Unknown result type (might be due to invalid IL or missing references)
                //IL_00f5: Expected O, but got Unknown
                //IL_00f5: Unknown result type (might be due to invalid IL or missing references)
                //IL_00fa: Unknown result type (might be due to invalid IL or missing references)
                //IL_0108: Expected O, but got Unknown
                LayoutView active = LayoutView.Active;
                if (active == null)
                {
                    ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("请先打开布局视图", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation)));
                }
                else
                {
                    Layout layout = active.Layout;
                    Element val = layout.FindElement("XF_DH");
                    if (val == null)
                    {
                        CIMColor val2 = CIMColor.CreateRGBColor(255.0, 0.0, 0.0, 100.0);
                        CIMTextSymbol val3 = SymbolFactory.Instance.ConstructTextSymbol(val2, 12.0, "Arial", "Regular");
                        MapPoint shape = MapPointBuilderEx.CreateMapPoint(-35.0, -40.0, (SpatialReference)null);
                        CIMTextGraphic val4 = new CIMTextGraphic
                        {
                            Shape = (Geometry)(object)shape,
                            Text = "J1",
                            Symbol = SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)val3)
                        };
                        ElementInfo val5 = new ElementInfo
                        {
                            CustomProperties = new List<CIMStringMap>()
                        };
                        GraphicElement val6 = ElementFactory.Instance.CreateGraphicElement((IElementContainer)(object)layout, (CIMGraphic)(object)val4, "XF_DH", true, val5);
                        ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("已创建点号文本模板（XF_DH）\n位于版面外（左下角负坐标），可设置字体、颜色等样式。", "成功", MessageBoxButton.OK, MessageBoxImage.Asterisk)));
                    }
                    else
                    {
                        ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("点号文本模板已存在（XF_DH），无需重复创建。\n可直接修改模板样式。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk)));
                    }
                }
            }, TaskCreationOptions.None);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            MessageBox.Show("创建点号模板失败: " + ex2.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
    }
    }
}
