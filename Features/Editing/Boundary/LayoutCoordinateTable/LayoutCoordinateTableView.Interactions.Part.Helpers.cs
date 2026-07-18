using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable
{
    public partial class LayoutCoordinateTableView
    {
    private async void InitializeAsync()
    {
        await RefreshLayoutsAsync();
        await RefreshLayersAsync();
        InitializeAreaUnits();
        AreaUnitComboBox.SelectionChanged += AreaUnitComboBox_SelectionChanged;
    }

    private void InitializeAreaUnits()
    {
        AreaUnitComboBox.Items.Clear();
        AreaUnitComboBox.Items.Add("平方米");
        AreaUnitComboBox.Items.Add("公顷");
        AreaUnitComboBox.SelectedIndex = 0;
    }

    private Task EnsureTemplatesAsync(string layoutName)
    {
        return QueuedTask.Run((Action)delegate
        {
            Project current = Project.Current;
            if (current == null)
            {
                throw new InvalidOperationException("未找到当前工程");
            }

            LayoutProjectItem val = current.GetItems<LayoutProjectItem>().FirstOrDefault((LayoutProjectItem l) => ((Item)l).Name == layoutName);
            if (val == null)
            {
                throw new InvalidOperationException("找不到布局: " + layoutName);
            }

            Layout layout = val.GetLayout();
            if (layout == null)
            {
                throw new InvalidOperationException("无法打开布局: " + layoutName);
            }

            Element val2 = layout.FindElement("XF_TX");
            if (val2 == null)
            {
                Element val3 = LayoutElementHelper.CreateRectangleTemplateSync(layout, "XF_TX", (X: -30.0, Y: -30.0), (Width: 50.0, Height: 20.0));
                ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
                {
                    LogMessage("已创建图形模板 XF_TX (白底、黑边，位于版面外)");
                }, Array.Empty<object>());
            }
            else
            {
                ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
                {
                    LogMessage("已存在图形模板 XF_TX");
                }, Array.Empty<object>());
            }

            Element val4 = layout.FindElement("XF_WB");
            if (val4 == null)
            {
                Element val5 = LayoutElementHelper.CreateTextTemplateSync(layout, "XF_WB", (X: -30.0, Y: -30.0), "模板文本");
                ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
                {
                    LogMessage("已创建文本模板 XF_WB (Arial, 黑色，位于版面外)");
                }, Array.Empty<object>());
            }
            else
            {
                ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
                {
                    LogMessage("已存在文本模板 XF_WB");
                }, Array.Empty<object>());
            }
        }, TaskCreationOptions.None);
    }

    private bool ValidateInputs()
    {
        if (LayoutComboBox.SelectedItem == null)
        {
            MessageBox.Show("请选择布局", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (LayerComboBox.SelectedItem == null)
        {
            MessageBox.Show("请选择面要素图层", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (UniqueFieldComboBox.SelectedItem == null)
        {
            MessageBox.Show("请选择唯一字段", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!int.TryParse(XYDecimalTextBox.Text, out var xyDecimal) || xyDecimal < 0 || xyDecimal > 10)
        {
            MessageBox.Show("坐标小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!int.TryParse(EdgeDecimalTextBox.Text, out var edgeDecimal) || edgeDecimal < 0 || edgeDecimal > 10)
        {
            MessageBox.Show("边长小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        string areaDecText = GetTextBoxText("AreaDecimalTextBox");
        if (!int.TryParse(areaDecText, out var areaDec) || areaDec < 0 || areaDec > 10)
        {
            MessageBox.Show("面积小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        string muDecText = GetTextBoxText("MuDecimalTextBox");
        if (!int.TryParse(muDecText, out var muDec) || muDec < 0 || muDec > 10)
        {
            MessageBox.Show("亩小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        string cornerOffsetText = GetTextBoxText("CornerOffsetTextBox");
        if (!double.TryParse(cornerOffsetText, NumberStyles.Float, CultureInfo.InvariantCulture, out var cornerOffset) || cornerOffset < 0.0)
        {
            MessageBox.Show("离角距离必须是非负数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        return true;
    }

    private List<(double X, double Y, string XStr, string YStr)> ExtractCoordinates(Polygon geometry, int decimalPlaces)
    {
        List<(double, double, string, string)> coordinates = new List<(double, double, string, string)>();
        ReadOnlyPointCollection points = ((Multipart)geometry).Points;
        foreach (MapPoint point in points)
        {
            coordinates.Add((point.X, point.Y, point.X.ToString($"F{decimalPlaces}"), point.Y.ToString($"F{decimalPlaces}")));
        }

        if (coordinates.Count > 0 && (coordinates[0].Item1 != coordinates[coordinates.Count - 1].Item1 || coordinates[0].Item2 != coordinates[coordinates.Count - 1].Item2))
        {
            (double, double, string, string) first = coordinates[0];
            coordinates.Add(first);
        }

        return coordinates;
    }
    }
}
