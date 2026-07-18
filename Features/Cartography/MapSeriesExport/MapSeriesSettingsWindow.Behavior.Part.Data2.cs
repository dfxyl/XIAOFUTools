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

    private void LoadSettings(CoordinateTableSettings settings)
    {
        EnableCoordinateTableCheckBox.IsChecked = settings.EnableCoordinateTable;
        TitleTextBox.Text = settings.Title ?? "";
        PrefixTextBox.Text = settings.Prefix ?? "J";
        DecimalTextBox.Text = settings.DecimalPlaces.ToString();
        GenerateEdgeCheckBox.IsChecked = settings.GenerateEdge;
        PointColWidthTextBox.Text = settings.PointColWidth.ToString(CultureInfo.InvariantCulture);
        XYColWidthTextBox.Text = settings.XYColWidth.ToString(CultureInfo.InvariantCulture);
        EdgeColWidthTextBox.Text = settings.EdgeColWidth.ToString(CultureInfo.InvariantCulture);
        RowHeightTextBox.Text = settings.RowHeight.ToString(CultureInfo.InvariantCulture);
        CornerOffsetTextBox.Text = settings.CornerOffset.ToString(CultureInfo.InvariantCulture);
        RowsPerColumnTextBox.Text = settings.RowsPerColumn.ToString();
        CompressTotalRowsTextBox.Text = settings.CompressTotalRows.ToString();
        AreaDecimalTextBox.Text = settings.AreaDecimal.ToString();
        MuDecimalTextBox.Text = settings.MuDecimal.ToString();
        if (settings.UseAnchorPosition)
        {
            AnchorPositionRadio.IsChecked = true;
            MapFramePanel.Visibility = Visibility.Collapsed;
            AnchorPanel.Visibility = Visibility.Visible;
        }
        else
        {
            MapFramePositionRadio.IsChecked = true;
            MapFramePanel.Visibility = Visibility.Visible;
            AnchorPanel.Visibility = Visibility.Collapsed;
        }

        if (!string.IsNullOrEmpty(settings.MapFrameName))
        {
            for (int i = 0; i < MapFrameComboBox.Items.Count; i++)
            {
                if (MapFrameComboBox.Items[i].ToString() == settings.MapFrameName)
                {
                    MapFrameComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        AnchorNameTextBox.Text = settings.AnchorElementName ?? "XF_MD";
        foreach (ComboBoxItem item in (IEnumerable)PlacementCornerComboBox.Items)
        {
            if (item.Content?.ToString() == settings.PlacementCorner)
            {
                PlacementCornerComboBox.SelectedItem = item;
                break;
            }
        }

        foreach (ComboBoxItem item2 in (IEnumerable)AreaModeComboBox.Items)
        {
            if (item2.Content?.ToString() == settings.AreaMode)
            {
                AreaModeComboBox.SelectedItem = item2;
                break;
            }
        }

        CustomAreaTextBox.Text = settings.CustomAreaText ?? "S=[面积] 平方米";
        ExportDelayTextBox.Text = settings.ExportDelay.ToString();
        EnableBoundaryPointsCheckBox.IsChecked = settings.EnableBoundaryPoints;
        BoundaryPointSizeTextBox.Text = settings.BoundaryPointSize.ToString(CultureInfo.InvariantCulture);
        EnablePointLabelsCheckBox.IsChecked = settings.EnablePointLabels;
        PointLabelPrefixTextBox.Text = settings.PointLabelPrefix ?? "J";
        PointLabelSuffixTextBox.Text = settings.PointLabelSuffix ?? "";
        PointLabelDistanceTextBox.Text = settings.PointLabelDistance.ToString(CultureInfo.InvariantCulture);
        PointLabelSizeTextBox.Text = settings.PointLabelSize.ToString(CultureInfo.InvariantCulture);
        foreach (ComboBoxItem item3 in (IEnumerable)PointLabelOverlapComboBox.Items)
        {
            if (item3.Content?.ToString() == settings.PointLabelOverlapMode)
            {
                PointLabelOverlapComboBox.SelectedItem = item3;
                break;
            }
        }

        EnableEdgeLabelsCheckBox.IsChecked = settings.EnableEdgeLabels;
        EdgeLabelPrefixTextBox.Text = settings.EdgeLabelPrefix ?? "";
        EdgeLabelSuffixTextBox.Text = settings.EdgeLabelSuffix ?? "";
        EdgeLabelDecimalTextBox.Text = settings.EdgeLabelDecimal.ToString();
        EdgeLabelPadZerosCheckBox.IsChecked = settings.EdgeLabelPadZeros;
        EdgeLabelDistanceTextBox.Text = settings.EdgeLabelDistance.ToString(CultureInfo.InvariantCulture);
        EdgeLabelSizeTextBox.Text = settings.EdgeLabelSize.ToString(CultureInfo.InvariantCulture);
        foreach (ComboBoxItem item4 in (IEnumerable)EdgeLabelOverlapComboBox.Items)
        {
            if (item4.Content?.ToString() == settings.EdgeLabelOverlapMode)
            {
                EdgeLabelOverlapComboBox.SelectedItem = item4;
                break;
            }
        }

        EnableIntersectTableCheckBox.IsChecked = settings.EnableIntersectTable;
        _pendingIntersectLayerName = settings.IntersectLayerName;
        _pendingIntersectFieldName = settings.IntersectClassField;
        IntersectTableTitleTextBox.Text = settings.IntersectTableTitle ?? "交集汇总表";
        SelectComboBoxItemByContent(IntersectAreaUnitComboBox, settings.IntersectAreaUnit);
        IntersectDecimalTextBox.Text = settings.IntersectDecimalPlaces.ToString();
        IntersectRowHeightTextBox.Text = settings.IntersectRowHeight.ToString(CultureInfo.InvariantCulture);
        IntersectCategoryColWidthTextBox.Text = settings.IntersectCategoryColWidth.ToString(CultureInfo.InvariantCulture);
        IntersectAreaColWidthTextBox.Text = settings.IntersectAreaColWidth.ToString(CultureInfo.InvariantCulture);
        SelectComboBoxItemByContent(IntersectPlacementCornerComboBox, settings.IntersectPlacementCorner);
        IntersectCornerOffsetTextBox.Text = settings.IntersectCornerOffset.ToString(CultureInfo.InvariantCulture);
        for (int j = 0; j < AreaUnitComboBox.Items.Count; j++)
        {
            if (AreaUnitComboBox.Items[j].ToString() == settings.AreaUnit)
            {
                AreaUnitComboBox.SelectedIndex = j;
                break;
            }
        }
    }

    }
}
