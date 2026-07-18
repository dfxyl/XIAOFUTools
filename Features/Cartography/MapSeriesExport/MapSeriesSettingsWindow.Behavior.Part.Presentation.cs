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

    private void RefreshMapFrames_Click(object sender, RoutedEventArgs e)
    {
        LoadMapFramesAsync();
    }

    private void RefreshIntersectLayers_Click(object sender, RoutedEventArgs e)
    {
        LoadIntersectLayersAsync();
    }

    private void IntersectLayerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadIntersectFieldsAsync(IntersectLayerComboBox.SelectedItem?.ToString());
    }

    private void PositionModeChanged(object sender, RoutedEventArgs e)
    {
        if (MapFramePanel != null && AnchorPanel != null)
        {
            if (MapFramePositionRadio.IsChecked == true)
            {
                MapFramePanel.Visibility = Visibility.Visible;
                AnchorPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                MapFramePanel.Visibility = Visibility.Collapsed;
                AnchorPanel.Visibility = Visibility.Visible;
            }
        }
    }

    private void AreaModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AutoAreaPanel != null && CustomAreaPanel != null)
        {
            switch (((!(AreaModeComboBox.SelectedItem is ComboBoxItem { Content: var content })) ? null : content?.ToString()) ?? "自动生成")
            {
                case "不生成":
                    AutoAreaPanel.Visibility = Visibility.Collapsed;
                    CustomAreaPanel.Visibility = Visibility.Collapsed;
                    break;
                case "自动生成":
                    AutoAreaPanel.Visibility = Visibility.Visible;
                    CustomAreaPanel.Visibility = Visibility.Collapsed;
                    break;
                case "自定义":
                    AutoAreaPanel.Visibility = Visibility.Collapsed;
                    CustomAreaPanel.Visibility = Visibility.Visible;
                    break;
            }
        }
    }

    private static void SelectComboBoxItemByContent(ComboBox comboBox, string content)
    {
        if (comboBox == null || string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        foreach (ComboBoxItem item in (IEnumerable)comboBox.Items)
        {
            if (item.Content?.ToString() == content)
            {
                comboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateInputs())
        {
            CoordinateTableSettings = new CoordinateTableSettings
            {
                EnableCoordinateTable = (EnableCoordinateTableCheckBox.IsChecked == true),
                LayerName = null,
                UniqueField = null,
                Title = TitleTextBox.Text,
                Prefix = PrefixTextBox.Text,
                DecimalPlaces = int.Parse(DecimalTextBox.Text),
                GenerateEdge = (GenerateEdgeCheckBox.IsChecked == true),
                UseAnchorPosition = (AnchorPositionRadio.IsChecked == true),
                MapFrameName = (MapFrameComboBox.SelectedItem?.ToString() ?? ""),
                AnchorElementName = (AnchorNameTextBox.Text?.Trim() ?? "XF_MD"),
                PointColWidth = double.Parse(PointColWidthTextBox.Text, CultureInfo.InvariantCulture),
                XYColWidth = double.Parse(XYColWidthTextBox.Text, CultureInfo.InvariantCulture),
                EdgeColWidth = double.Parse(EdgeColWidthTextBox.Text, CultureInfo.InvariantCulture),
                RowHeight = double.Parse(RowHeightTextBox.Text, CultureInfo.InvariantCulture),
                PlacementCorner = ((PlacementCornerComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "左下角"),
                CornerOffset = double.Parse(CornerOffsetTextBox.Text, CultureInfo.InvariantCulture),
                AreaMode = ((AreaModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "自动生成"),
                CustomAreaText = (CustomAreaTextBox.Text ?? "S=[面积] 平方米"),
                AreaUnit = (AreaUnitComboBox.SelectedItem?.ToString() ?? "平方米"),
                AreaDecimal = int.Parse(AreaDecimalTextBox.Text),
                MuDecimal = int.Parse(MuDecimalTextBox.Text),
                RowsPerColumn = int.Parse(RowsPerColumnTextBox.Text),
                CompressTotalRows = int.Parse(CompressTotalRowsTextBox.Text),
                ExportDelay = int.Parse(ExportDelayTextBox.Text),
                EnableBoundaryPoints = (EnableBoundaryPointsCheckBox.IsChecked == true),
                BoundaryPointSize = double.Parse(BoundaryPointSizeTextBox.Text, CultureInfo.InvariantCulture),
                EnablePointLabels = (EnablePointLabelsCheckBox.IsChecked == true),
                PointLabelPrefix = (PointLabelPrefixTextBox.Text ?? "J"),
                PointLabelSuffix = (PointLabelSuffixTextBox.Text ?? ""),
                PointLabelDistance = double.Parse(PointLabelDistanceTextBox.Text, CultureInfo.InvariantCulture),
                PointLabelSize = double.Parse(PointLabelSizeTextBox.Text, CultureInfo.InvariantCulture),
                PointLabelOverlapMode = ((PointLabelOverlapComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "压盖隐藏"),
                EnableEdgeLabels = (EnableEdgeLabelsCheckBox.IsChecked == true),
                EdgeLabelPrefix = (EdgeLabelPrefixTextBox.Text ?? ""),
                EdgeLabelSuffix = (EdgeLabelSuffixTextBox.Text ?? ""),
                EdgeLabelDecimal = int.Parse(EdgeLabelDecimalTextBox.Text),
                EdgeLabelPadZeros = (EdgeLabelPadZerosCheckBox.IsChecked == true),
                EdgeLabelDistance = double.Parse(EdgeLabelDistanceTextBox.Text, CultureInfo.InvariantCulture),
                EdgeLabelSize = double.Parse(EdgeLabelSizeTextBox.Text, CultureInfo.InvariantCulture),
                EdgeLabelOverlapMode = ((EdgeLabelOverlapComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "压盖隐藏"),
                EnableIntersectTable = (EnableIntersectTableCheckBox.IsChecked == true),
                IntersectLayerName = (IntersectLayerComboBox.SelectedItem?.ToString() ?? ""),
                IntersectClassField = (IntersectFieldComboBox.SelectedItem?.ToString() ?? ""),
                IntersectTableTitle = (IntersectTableTitleTextBox.Text ?? "交集汇总表"),
                IntersectAreaUnit = ((IntersectAreaUnitComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "平方米"),
                IntersectDecimalPlaces = int.Parse(IntersectDecimalTextBox.Text),
                IntersectCategoryColWidth = double.Parse(IntersectCategoryColWidthTextBox.Text, CultureInfo.InvariantCulture),
                IntersectAreaColWidth = double.Parse(IntersectAreaColWidthTextBox.Text, CultureInfo.InvariantCulture),
                IntersectRowHeight = double.Parse(IntersectRowHeightTextBox.Text, CultureInfo.InvariantCulture),
                IntersectPlacementCorner = ((IntersectPlacementCornerComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "右下角"),
                IntersectCornerOffset = double.Parse(IntersectCornerOffsetTextBox.Text, CultureInfo.InvariantCulture)
            };
            SaveSettingsToFile(CoordinateTableSettings);
            this.SettingsSaved?.Invoke(CoordinateTableSettings);
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    }
}
