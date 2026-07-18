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

    private async void CreateAnchor_Click(object sender, RoutedEventArgs e)
    {
        string anchorName = AnchorNameTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(anchorName))
        {
            MessageBox.Show("请输入锚点名称", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return;
        }

        try
        {
            await QueuedTask.Run((Action)delegate
            {
                LayoutView active = LayoutView.Active;
                if (active == null)
                {
                    ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("请先打开布局视图", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation)));
                }
                else
                {
                    Layout layout = active.Layout;
                    Element val = layout.FindElement(anchorName);
                    if (val != null)
                    {
                        ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("布局中已存在名为 '" + anchorName + "' 的元素", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk)));
                    }
                    else
                    {
                        double width = layout.GetPage().Width;
                        double height = layout.GetPage().Height;
                        double num = width / 2.0;
                        double num2 = height / 2.0;
                        MapPoint val2 = MapPointBuilderEx.CreateMapPoint(num, num2, (SpatialReference)null);
                        CIMPointSymbol val3 = SymbolFactory.Instance.ConstructPointSymbol(ColorFactory.Instance.RedRGB, 4.0, (SimpleMarkerStyle)0);
                        GraphicElement val4 = ElementFactory.Instance.CreateGraphicElement((IElementContainer)(object)layout, (Geometry)(object)val2, (CIMSymbol)(object)val3, anchorName, true, (ElementInfo)null);
                        ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("锚点 '" + anchorName + "' 创建成功！\n请在布局中将其移动到所需位置。", "成功", MessageBoxButton.OK, MessageBoxImage.Asterisk)));
                    }
                }
            }, TaskCreationOptions.None);
        }
        catch (Exception ex)
        {
            MessageBox.Show("创建锚点失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
    }

    private async void CreateTemplates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await QueuedTask.Run((Action)delegate
            {
                LayoutView active = LayoutView.Active;
                if (active == null)
                {
                    ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("请先打开布局视图", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation)));
                }
                else
                {
                    Layout layout = active.Layout;
                    bool txCreated = false;
                    bool wbCreated = false;
                    Element val = layout.FindElement("XF_TX");
                    if (val == null)
                    {
                        LayoutElementHelper.CreateRectangleTemplateSync(layout, "XF_TX", (X: -30.0, Y: -30.0), (Width: 50.0, Height: 20.0));
                        txCreated = true;
                    }

                    Element val2 = layout.FindElement("XF_WB");
                    if (val2 == null)
                    {
                        LayoutElementHelper.CreateTextTemplateSync(layout, "XF_WB", (X: -30.0, Y: -55.0), "模板文本");
                        wbCreated = true;
                    }

                    ((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)delegate
                    {
                        if (txCreated || wbCreated)
                        {
                            string text = "已创建模板：";
                            if (txCreated)
                            {
                                text += "\n• XF_TX（图形模板）";
                            }

                            if (wbCreated)
                            {
                                text += "\n• XF_WB（文本模板）";
                            }

                            text += "\n\n模板位于版面外（左下角负坐标），可设置颜色、字体等样式后生成时将复制使用。";
                            MessageBox.Show(text, "成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                        }
                        else
                        {
                            MessageBox.Show("模板已存在（XF_TX、XF_WB），无需重复创建。\n可直接修改模板样式。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                        }
                    });
                }
            }, TaskCreationOptions.None);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            MessageBox.Show("创建模板失败: " + ex2.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
    }

    private void SaveSettingsToFile(CoordinateTableSettings settings)
    {
        _settingsStore.Save(settings);
    }

    private async void CreatePointTemplate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await QueuedTask.Run((Action)delegate
            {
                //IL_00c2: Unknown result type (might be due to invalid IL or missing references)
                //IL_00c7: Unknown result type (might be due to invalid IL or missing references)
                //IL_00d0: Unknown result type (might be due to invalid IL or missing references)
                //IL_00e0: Expected O, but got Unknown
                //IL_00e0: Unknown result type (might be due to invalid IL or missing references)
                //IL_00e5: Unknown result type (might be due to invalid IL or missing references)
                //IL_00f3: Expected O, but got Unknown
                LayoutView active = LayoutView.Active;
                if (active == null)
                {
                    ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("请先打开布局视图", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation)));
                }
                else
                {
                    Layout layout = active.Layout;
                    Element val = layout.FindElement("XF_JZD");
                    if (val == null)
                    {
                        CIMColor val2 = CIMColor.CreateRGBColor(255.0, 0.0, 0.0, 100.0);
                        CIMPointSymbol val3 = SymbolFactory.Instance.ConstructPointSymbol(val2, 8.0, (SimpleMarkerStyle)0);
                        MapPoint location = MapPointBuilderEx.CreateMapPoint(-35.0, -35.0, (SpatialReference)null);
                        CIMPointGraphic val4 = new CIMPointGraphic
                        {
                            Location = location,
                            Symbol = SymbolExtensionMethods.MakeSymbolReference((CIMSymbol)(object)val3)
                        };
                        ElementInfo val5 = new ElementInfo
                        {
                            CustomProperties = new List<CIMStringMap>()
                        };
                        GraphicElement val6 = ElementFactory.Instance.CreateGraphicElement((IElementContainer)(object)layout, (CIMGraphic)(object)val4, "XF_JZD", true, val5);
                        ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("已创建界址点模板（XF_JZD）\n位于版面外（左下角负坐标），可设置颜色、大小等样式。", "成功", MessageBoxButton.OK, MessageBoxImage.Asterisk)));
                    }
                    else
                    {
                        ((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show("界址点模板已存在（XF_JZD），无需重复创建。\n可直接修改模板样式。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk)));
                    }
                }
            }, TaskCreationOptions.None);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            MessageBox.Show("创建界址点模板失败: " + ex2.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
    }
    }
}
