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
    private void InitializeAreaUnits()
    {
        AreaUnitComboBox.Items.Clear();
        AreaUnitComboBox.Items.Add("平方米");
        AreaUnitComboBox.Items.Add("公顷");
        AreaUnitComboBox.SelectedIndex = 0;
    }

    private bool ValidateInputs()
    {
        if (!int.TryParse(DecimalTextBox.Text, out var dec) || dec < 0 || dec > 10)
        {
            MessageBox.Show("小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(PointColWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pw) || pw <= 0.0)
        {
            MessageBox.Show("点号列宽必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(XYColWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var xyw) || xyw <= 0.0)
        {
            MessageBox.Show("坐标列宽必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(EdgeColWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var ew) || ew <= 0.0)
        {
            MessageBox.Show("边长列宽必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(RowHeightTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var rh) || rh <= 0.0)
        {
            MessageBox.Show("行高必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(CornerOffsetTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var co) || co < 0.0)
        {
            MessageBox.Show("偏移量必须是非负数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!int.TryParse(RowsPerColumnTextBox.Text, out var rpc) || rpc < 1)
        {
            MessageBox.Show("每列行数必须是正整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!int.TryParse(CompressTotalRowsTextBox.Text, out var ctr) || ctr < 0)
        {
            MessageBox.Show("压缩总行数必须是大于等于0的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!int.TryParse(ExportDelayTextBox.Text, out var delay) || delay < 0 || delay > 10000)
        {
            MessageBox.Show("等待时间必须是0-10000之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!int.TryParse(AreaDecimalTextBox.Text, out var ad) || ad < 0 || ad > 10)
        {
            MessageBox.Show("面积小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!int.TryParse(MuDecimalTextBox.Text, out var md) || md < 0 || md > 10)
        {
            MessageBox.Show("亩位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(BoundaryPointSizeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var bps) || bps <= 0.0)
        {
            MessageBox.Show("界址点大小必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(PointLabelDistanceTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pld) || pld < 0.0)
        {
            MessageBox.Show("点号距离必须是非负数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(PointLabelSizeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pls) || pls <= 0.0)
        {
            MessageBox.Show("点号大小必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!int.TryParse(EdgeLabelDecimalTextBox.Text, out var eld) || eld < 0 || eld > 10)
        {
            MessageBox.Show("边长小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(EdgeLabelDistanceTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var eldi) || eldi < 0.0)
        {
            MessageBox.Show("边长距离必须是非负数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(EdgeLabelSizeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var els) || els <= 0.0)
        {
            MessageBox.Show("边长字体大小必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (EnableIntersectTableCheckBox.IsChecked == true)
        {
            if (IntersectLayerComboBox.SelectedItem == null)
            {
                MessageBox.Show("请选择交集表格相交图层", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            if (IntersectFieldComboBox.SelectedItem == null)
            {
                MessageBox.Show("请选择交集表格分类字段", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
        }

        if (!int.TryParse(IntersectDecimalTextBox.Text, out var itd) || itd < 0 || itd > 10)
        {
            MessageBox.Show("交集表小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(IntersectCategoryColWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var itcw) || itcw <= 0.0)
        {
            MessageBox.Show("交集表类别列宽必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(IntersectAreaColWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var itaw) || itaw <= 0.0)
        {
            MessageBox.Show("交集表面积列宽必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(IntersectRowHeightTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var itrh) || itrh <= 0.0)
        {
            MessageBox.Show("交集表行高必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        if (!double.TryParse(IntersectCornerOffsetTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var itco) || itco < 0.0)
        {
            MessageBox.Show("交集表偏移量必须是非负数", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return false;
        }

        return true;
    }
    }
}
