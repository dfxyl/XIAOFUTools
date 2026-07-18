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

    private async void CreateTemplatesButton_Click(object sender, RoutedEventArgs e)
    {
        string layoutName = LayoutComboBox.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(layoutName))
        {
            MessageBox.Show("请选择布局", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            return;
        }

        CreateTemplatesButton.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;
        LogMessage("开始检查并生成/复制模板...");
        try
        {
            await EnsureTemplatesAsync(layoutName);
            LogMessage("模板已准备就绪：XF_TX（图形）、XF_WB（文本）");
            MessageBox.Show("模板已准备就绪（若不存在则已创建）。", "完成", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            LogMessage("生成/复制模板失败: " + ex2.Message);
            MessageBox.Show("生成/复制模板失败：" + ex2.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
        finally
        {
            CreateTemplatesButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInputs())
        {
            return;
        }

        GenerateButton.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;
        LogTextBox.Clear();
        try
        {
            await GenerateCoordinateTableAsync();
            LogMessage("坐标表生成完成！");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            LogMessage("生成坐标表时发生错误: " + ex2.Message);
            MessageBox.Show("生成坐标表时发生错误：" + ex2.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
        finally
        {
            GenerateButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async Task GenerateCoordinateTableAsync()
    {
        string layoutName = LayoutComboBox.SelectedItem?.ToString();
        string layerName = LayerComboBox.SelectedItem?.ToString();
        string uniqueField = UniqueFieldComboBox.SelectedItem?.ToString();
        string titleText = TitleTextBox.Text;
        string prefixStr = PrefixTextBox.Text;
        int xyDecimal = int.Parse(XYDecimalTextBox.Text);
        bool generateEdge = GenerateEdgeCheckBox.IsChecked == true;
        int edgeDecimal = int.Parse(EdgeDecimalTextBox.Text);
        double pointColWidth = double.Parse(PointColWidthTextBox.Text, CultureInfo.InvariantCulture);
        double xyColWidth = double.Parse(XYColWidthTextBox.Text, CultureInfo.InvariantCulture);
        double edgeColWidth = double.Parse(EdgeColWidthTextBox.Text, CultureInfo.InvariantCulture);
        double rowHeight = double.Parse(RowHeightTextBox.Text, CultureInfo.InvariantCulture);
        string areaUnit = AreaUnitComboBox.SelectedItem?.ToString() ?? "平方米";
        string areaDecimalText = GetTextBoxText("AreaDecimalTextBox");
        int areaDecimal = int.Parse(areaDecimalText);
        string muDecimalText = GetTextBoxText("MuDecimalTextBox");
        int muDecimal = int.Parse(muDecimalText);
        string placementCorner = ((FindName("PlacementCornerComboBox") as ComboBox)?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "左下角";
        string cornerOffsetText2 = GetTextBoxText("CornerOffsetTextBox");
        double cornerOffset = double.Parse(cornerOffsetText2, CultureInfo.InvariantCulture);
        int rowsPerColumn = int.Parse(RowsPerColumnTextBox.Text);
        bool surveyStyle = SwapXYCheckBox?.IsChecked == true;
        await QueuedTask.Run((Func<Task>)async delegate
        {
            try
            {
                Project project = Project.Current;
                LayoutProjectItem layoutItem = project.GetItems<LayoutProjectItem>().FirstOrDefault((LayoutProjectItem l) => ((Item)l).Name == layoutName);
                if (layoutItem == null)
                {
                    ((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)delegate
                    {
                        LogMessage("找不到布局: " + layoutName);
                    });
                }
                else
                {
                    Layout layout = layoutItem.GetLayout();
                    MapView mapView = MapView.Active;
                    FeatureLayer layer = mapView.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault((FeatureLayer l) => ((MapMember)l).Name == layerName);
                    if (layer == null)
                    {
                        ((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)delegate
                        {
                            LogMessage("找不到图层: " + layerName);
                        });
                    }
                    else
                    {
                        try
                        {
                            Table table = ((BasicFeatureLayer)layer).GetTable();
                            try
                            {
                                int processedCount = 0;
                                SelectionSet selection = mapView.Map.GetSelection();
                                Dictionary<MapMember, List<long>> selectionDict = ((selection != null) ? ((MapMemberIDSet)selection).ToDictionary() : null);
                                List<long> selectedOids = null;
                                if (selectionDict != null)
                                {
                                    foreach (KeyValuePair<MapMember, List<long>> kvp in selectionDict)
                                    {
                                        MapMember key = kvp.Key;
                                        FeatureLayer fl = (FeatureLayer)(object)((key is FeatureLayer) ? key : null);
                                        if (fl != null && ((MapMember)fl).Name == layerName)
                                        {
                                            selectedOids = kvp.Value;
                                            break;
                                        }
                                    }
                                }

                                RowCursor cursor;
                                if (selectedOids != null && selectedOids.Count > 0)
                                {
                                    TableDefinition def = table.GetDefinition();
                                    string oidField = def.GetObjectIDField();
                                    string inClause = string.Join(",", selectedOids);
                                    QueryFilter qf = new QueryFilter
                                    {
                                        WhereClause = oidField + " IN (" + inClause + ")"
                                    };
                                    cursor = table.Search(qf, true);
                                }
                                else
                                {
                                    cursor = table.Search((QueryFilter)null, true);
                                }

                                RowCursor val = cursor;
                                try
                                {
                                    while (cursor.MoveNext())
                                    {
                                        Row row = cursor.Current;
                                        try
                                        {
                                            object obj = row["SHAPE"];
                                            Polygon geometry = (Polygon)((obj is Polygon) ? obj : null);
                                            string uniqueValue = row[uniqueField]?.ToString();
                                            if (geometry != null && !string.IsNullOrEmpty(uniqueValue))
                                            {
                                                await ProcessPolygonAsync(layout, geometry, uniqueValue, titleText, prefixStr, xyDecimal, generateEdge, edgeDecimal, pointColWidth, xyColWidth, edgeColWidth, rowHeight, areaUnit, areaDecimal, muDecimal, rowsPerColumn, placementCorner, cornerOffset, surveyStyle);
                                                processedCount++;
                                                string logMsg = $"已处理面要素: {uniqueValue} ({processedCount})";
                                                _ = ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
                                                {
                                                    LogMessage(logMsg);
                                                }, Array.Empty<object>());
                                                await Task.Delay(100);
                                            }
                                        }
                                        finally
                                        {
                                            ((IDisposable)row)?.Dispose();
                                        }
                                    }
                                }
                                finally
                                {
                                    ((IDisposable)val)?.Dispose();
                                }
                            }
                            finally
                            {
                                ((IDisposable)table)?.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            Exception ex2 = ex;
                            Exception ex3 = ex2;
                            _ = ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
                            {
                                LogMessage("处理过程中发生错误: " + ex3.Message);
                            }, Array.Empty<object>());
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex4)
            {
                Exception ex2 = ex4;
                Exception ex5 = ex2;
                _ = ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
                {
                    LogMessage("生成坐标表时发生错误: " + ex5.Message);
                }, Array.Empty<object>());
                throw;
            }
        }, TaskCreationOptions.None);
    }
    }
}
