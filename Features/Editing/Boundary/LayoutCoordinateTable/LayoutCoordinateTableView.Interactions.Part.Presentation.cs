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

    private void AreaUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        string unit = AreaUnitComboBox.SelectedItem?.ToString();
        if (FindName("AreaDecimalTextBox")is TextBox tb)
        {
            tb.Text = ((unit == "公顷") ? "4" : "2");
        }
    }

    private async Task RefreshLayoutsAsync()
    {
        try
        {
            LayoutComboBox.Items.Clear();
            Project project = Project.Current;
            if (project == null)
            {
                return;
            }

            await QueuedTask.Run((Action)delegate
            {
                IEnumerable<LayoutProjectItem> layouts = project.GetItems<LayoutProjectItem>();
                ((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)delegate
                {
                    foreach (LayoutProjectItem current in layouts)
                    {
                        LayoutComboBox.Items.Add(((Item)current).Name);
                    }

                    if (LayoutComboBox.Items.Count > 0)
                    {
                        LayoutComboBox.SelectedIndex = 0;
                    }
                });
            }, TaskCreationOptions.None);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            LogMessage("刷新布局列表失败: " + ex2.Message);
        }
    }

    private async Task RefreshLayersAsync()
    {
        try
        {
            LayerComboBox.Items.Clear();
            UniqueFieldComboBox.Items.Clear();
            MapView mapView = MapView.Active;
            if (mapView == null)
            {
                return;
            }

            await QueuedTask.Run((Action)delegate
            {
                IEnumerable<FeatureLayer> polygonLayers =
                    from layer in mapView.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                    where (int)((BasicFeatureLayer)layer).ShapeType == 4
                    select layer;
                ((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)delegate
                {
                    foreach (FeatureLayer current in polygonLayers)
                    {
                        LayerComboBox.Items.Add(((MapMember)current).Name);
                    }

                    if (LayerComboBox.Items.Count > 0)
                    {
                        LayerComboBox.SelectedIndex = 0;
                    }
                });
            }, TaskCreationOptions.None);
            if (LayerComboBox.Items.Count > 0)
            {
                await RefreshFieldsAsync();
            }
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            LogMessage("刷新图层列表失败: " + ex2.Message);
        }
    }

    private async Task RefreshFieldsAsync()
    {
        try
        {
            UniqueFieldComboBox.Items.Clear();
            if (LayerComboBox.SelectedItem == null)
            {
                return;
            }

            string layerName = LayerComboBox.SelectedItem.ToString();
            MapView mapView = MapView.Active;
            if (mapView == null)
            {
                return;
            }

            await QueuedTask.Run((Action)delegate
            {
                FeatureLayer val = mapView.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault((FeatureLayer l) => ((MapMember)l).Name == layerName);
                if (val != null)
                {
                    Table table = ((BasicFeatureLayer)val).GetTable();
                    try
                    {
                        TableDefinition definition = table.GetDefinition();
                        IEnumerable<Field> fields =
                            from f in definition.GetFields()
                            where (int)f.FieldType == 4 || (int)f.FieldType == 1 || (int)f.FieldType == 0
                            select f;
                        ((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)delegate
                        {
                            foreach (Field current in fields)
                            {
                                UniqueFieldComboBox.Items.Add(current.Name);
                            }

                            if (UniqueFieldComboBox.Items.Count > 0)
                            {
                                UniqueFieldComboBox.SelectedIndex = 0;
                            }
                        });
                    }
                    finally
                    {
                        ((IDisposable)table)?.Dispose();
                    }
                }
            }, TaskCreationOptions.None);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            LogMessage("刷新字段列表失败: " + ex2.Message);
        }
    }

    private void RefreshLayoutsButton_Click(object sender, RoutedEventArgs e)
    {
        _ = RefreshLayoutsAsync();
    }

    private void RefreshLayersButton_Click(object sender, RoutedEventArgs e)
    {
        _ = RefreshLayersAsync();
    }

    private void LayerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = RefreshFieldsAsync();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        string helpText = "布局生成坐标表[要素图层版] 使用说明：\r\n\r\n1. 选择目标布局和面要素图层\r\n2. 选择用于命名组的唯一字段\r\n3. 设置标题文本和点号前缀\r\n4. 配置坐标和边长的小数位数\r\n5. 设置表格列宽和行高（单位：毫米）\r\n6. 选择面积显示单位\r\n7. 设置每列显示的数据行数\r\n\r\n功能说明：\r\n- 自动遍历面要素生成坐标表\r\n- 支持坐标表分列显示\r\n- 可选择是否生成边长列\r\n- 自动计算面积并显示\r\n- 支持自定义表格样式\r\n\r\n注意事项：\r\n- 需要在布局中预设文本模板'XF_WB'和图形模板'XF_TX'\r\n- 表格会按照面要素的唯一字段值进行分组\r\n- 生成的表格会自动定位到地图框内";
        MessageBox.Show(helpText, "帮助", MessageBoxButton.OK, MessageBoxImage.Asterisk);
    }

    private async Task ProcessPolygonAsync(Layout layout, Polygon geometry, string uniqueValue, string titleText, string prefixStr, int xyDecimal, bool generateEdge, int edgeDecimal, double pointColWidth, double xyColWidth, double edgeColWidth, double rowHeight, string areaUnit, int areaDecimal, int muDecimal, int rowsPerColumn, string placementCorner, double cornerOffset, bool surveyStyle)
    {
        try
        {
            List<(double X, double Y, string XStr, string YStr)> coordinates = ExtractCoordinates(geometry, xyDecimal);
            List<double> edgeLengths = CalculateEdgeLengths(coordinates);
            (string, string) tuple = FormatArea(((Multipart)geometry).Area, areaUnit, areaDecimal, muDecimal);
            string areaFormatted = tuple.Item1;
            string muAreaFormatted = tuple.Item2;
            List<string> tableData = GenerateTableData(coordinates, titleText, prefixStr, generateEdge, edgeLengths, edgeDecimal, rowsPerColumn, surveyStyle);
            await CreateTableElementsAsync(layout, tableData, uniqueValue, areaFormatted, muAreaFormatted, areaUnit, pointColWidth, xyColWidth, edgeColWidth, rowHeight, generateEdge, placementCorner, cornerOffset);
            _ = Task.Run(delegate
            {
                LogMessage("面要素 " + uniqueValue + " 的坐标表已生成");
            });
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            Exception ex3 = ex2;
            _ = Task.Run(delegate
            {
                LogMessage("处理面要素 " + uniqueValue + " 时发生错误: " + ex3.Message);
            });
        }
    }

    private void LogMessage(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string logEntry = $"[{timestamp}] {message}\r\n";
        if (((DispatcherObject)LogTextBox).Dispatcher.CheckAccess())
        {
            LogTextBox.AppendText(logEntry);
            LogTextBox.ScrollToEnd();
            return;
        }

        ((DispatcherObject)LogTextBox).Dispatcher.Invoke((Action)delegate
        {
            LogTextBox.AppendText(logEntry);
            LogTextBox.ScrollToEnd();
        });
    }
    }
}
