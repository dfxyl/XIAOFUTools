using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;

namespace XIAOFUTools.Tools.Edit.Boundary.LayoutCoordinateTable
{
    public partial class LayoutCoordinateTableView : UserControl
    {
        public LayoutCoordinateTableView()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            await RefreshLayoutsAsync();
            await RefreshLayersAsync();
            InitializeAreaUnits();
            // 面积单位选择变更时，按单位设置默认小数位：平方米=2、公顷=4
            AreaUnitComboBox.SelectionChanged += AreaUnitComboBox_SelectionChanged;
        }

        private void InitializeAreaUnits()
        {
            AreaUnitComboBox.Items.Clear();
            AreaUnitComboBox.Items.Add("平方米");
            AreaUnitComboBox.Items.Add("公顷");
            AreaUnitComboBox.SelectedIndex = 0;
        }

        private void AreaUnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var unit = AreaUnitComboBox.SelectedItem?.ToString();
            var tb = FindName("AreaDecimalTextBox") as TextBox;
            if (tb == null) return;
            tb.Text = unit == "公顷" ? "4" : "2";
        }

        // 兼容某些环境下 XAML 生成字段未及时同步的情况，运行时按名称查找控件
        private string GetTextBoxText(string name)
        {
            var tb = FindName(name) as TextBox;
            return tb?.Text ?? string.Empty;
        }

        private async Task RefreshLayoutsAsync()
        {
            try
            {
                LayoutComboBox.Items.Clear();
                
                var project = ArcGIS.Desktop.Core.Project.Current;
                if (project == null) return;

                await QueuedTask.Run(() =>
                {
                    var layouts = project.GetItems<LayoutProjectItem>();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var layout in layouts)
                        {
                            LayoutComboBox.Items.Add(layout.Name);
                        }
                        if (LayoutComboBox.Items.Count > 0)
                            LayoutComboBox.SelectedIndex = 0;
                    });
                });
            }
            catch (Exception ex)
            {
                LogMessage($"刷新布局列表失败: {ex.Message}");
            }
        }

        private async void CreateTemplatesButton_Click(object sender, RoutedEventArgs e)
        {
            var layoutName = LayoutComboBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(layoutName))
            {
                MessageBox.Show("请选择布局", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CreateTemplatesButton.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            LogMessage("开始检查并生成/复制模板...");

            try
            {
                await EnsureTemplatesAsync(layoutName);
                LogMessage("模板已准备就绪：XF_TX（图形）、XF_WB（文本）");
                MessageBox.Show("模板已准备就绪（若不存在则已创建）。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"生成/复制模板失败: {ex.Message}");
                MessageBox.Show($"生成/复制模板失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CreateTemplatesButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private Task EnsureTemplatesAsync(string layoutName)
        {
            return QueuedTask.Run(() =>
            {
                var project = ArcGIS.Desktop.Core.Project.Current;
                if (project == null)
                    throw new InvalidOperationException("未找到当前工程");

                var layoutItem = project.GetItems<LayoutProjectItem>().FirstOrDefault(l => l.Name == layoutName);
                if (layoutItem == null)
                    throw new InvalidOperationException($"找不到布局: {layoutName}");

                var layout = layoutItem.GetLayout();
                if (layout == null)
                    throw new InvalidOperationException($"无法打开布局: {layoutName}");

                // 查找并创建 XF_TX（图形模板）
                var tx = layout.FindElement("XF_TX");
                if (tx == null)
                {
                    // 放在版面外以免干扰页面内容，使用负坐标；尺寸 50x20mm
                    var rect = LayoutElementHelper.CreateRectangleTemplateSync(
                        layout,
                        name: "XF_TX",
                        position: (-30, -30),
                        size: (50, 20),
                        symbol: null);
                    Application.Current.Dispatcher.BeginInvoke(() => LogMessage("已创建图形模板 XF_TX (白底、黑边，位于版面外)"));
                }
                else
                {
                    Application.Current.Dispatcher.BeginInvoke(() => LogMessage("已存在图形模板 XF_TX"));
                }

                // 查找并创建 XF_WB（文本模板）
                var wb = layout.FindElement("XF_WB");
                if (wb == null)
                {
                    var text = LayoutElementHelper.CreateTextTemplateSync(
                        layout,
                        name: "XF_WB",
                        position: (-30, -30),
                        text: "模板文本",
                        symbol: null);
                    Application.Current.Dispatcher.BeginInvoke(() => LogMessage("已创建文本模板 XF_WB (Arial, 黑色，位于版面外)"));
                }
                else
                {
                    Application.Current.Dispatcher.BeginInvoke(() => LogMessage("已存在文本模板 XF_WB"));
                }
            });
        }

        private async Task RefreshLayersAsync()
        {
            try
            {
                LayerComboBox.Items.Clear();
                UniqueFieldComboBox.Items.Clear();

                var mapView = MapView.Active;
                if (mapView == null) return;

                await QueuedTask.Run(() =>
                {
                    var polygonLayers = mapView.Map.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .Where(layer => layer.ShapeType == esriGeometryType.esriGeometryPolygon);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var layer in polygonLayers)
                        {
                            LayerComboBox.Items.Add(layer.Name);
                        }
                        if (LayerComboBox.Items.Count > 0)
                            LayerComboBox.SelectedIndex = 0;
                    });
                });

                if (LayerComboBox.Items.Count > 0)
                {
                    await RefreshFieldsAsync();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"刷新图层列表失败: {ex.Message}");
            }
        }

        private async Task RefreshFieldsAsync()
        {
            try
            {
                UniqueFieldComboBox.Items.Clear();

                if (LayerComboBox.SelectedItem == null) return;

                string layerName = LayerComboBox.SelectedItem.ToString();
                var mapView = MapView.Active;
                if (mapView == null) return;

                await QueuedTask.Run(() =>
                {
                    var layer = mapView.Map.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .FirstOrDefault(l => l.Name == layerName);

                    if (layer != null)
                    {
                        using (var table = layer.GetTable())
                        {
                            var tableDefinition = table.GetDefinition();
                            var fields = tableDefinition.GetFields()
                                .Where(f => f.FieldType == FieldType.String || 
                                           f.FieldType == FieldType.Integer || 
                                           f.FieldType == FieldType.SmallInteger);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                foreach (var field in fields)
                                {
                                    UniqueFieldComboBox.Items.Add(field.Name);
                                }
                                if (UniqueFieldComboBox.Items.Count > 0)
                                    UniqueFieldComboBox.SelectedIndex = 0;
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogMessage($"刷新字段列表失败: {ex.Message}");
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

        private void LayoutCoordinateTableView_Loaded(object sender, RoutedEventArgs e)
        {
            LayerComboBox.SelectionChanged += LayerComboBox_SelectionChanged;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string helpText = @"布局生成坐标表[要素图层版] 使用说明：

1. 选择目标布局和面要素图层
2. 选择用于命名组的唯一字段
3. 设置标题文本和点号前缀
4. 配置坐标和边长的小数位数
5. 设置表格列宽和行高（单位：毫米）
6. 选择面积显示单位
7. 设置每列显示的数据行数

功能说明：
- 自动遍历面要素生成坐标表
- 支持坐标表分列显示
- 可选择是否生成边长列
- 自动计算面积并显示
- 支持自定义表格样式

注意事项：
- 需要在布局中预设文本模板'XF_WB'和图形模板'XF_TX'
- 表格会按照面要素的唯一字段值进行分组
- 生成的表格会自动定位到地图框内";

            MessageBox.Show(helpText, "帮助", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
                return;

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
                LogMessage($"生成坐标表时发生错误: {ex.Message}");
                MessageBox.Show($"生成坐标表时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                GenerateButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private bool ValidateInputs()
        {
            if (LayoutComboBox.SelectedItem == null)
            {
                MessageBox.Show("请选择布局", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (LayerComboBox.SelectedItem == null)
            {
                MessageBox.Show("请选择面要素图层", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (UniqueFieldComboBox.SelectedItem == null)
            {
                MessageBox.Show("请选择唯一字段", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(XYDecimalTextBox.Text, out int xyDecimal) || xyDecimal < 0 || xyDecimal > 10)
            {
                MessageBox.Show("坐标小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(EdgeDecimalTextBox.Text, out int edgeDecimal) || edgeDecimal < 0 || edgeDecimal > 10)
            {
                MessageBox.Show("边长小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var areaDecText = GetTextBoxText("AreaDecimalTextBox");
            if (!int.TryParse(areaDecText, out int areaDec) || areaDec < 0 || areaDec > 10)
            {
                MessageBox.Show("面积小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var muDecText = GetTextBoxText("MuDecimalTextBox");
            if (!int.TryParse(muDecText, out int muDec) || muDec < 0 || muDec > 10)
            {
                MessageBox.Show("亩小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var cornerOffsetText = GetTextBoxText("CornerOffsetTextBox");
            if (!double.TryParse(cornerOffsetText, NumberStyles.Float, CultureInfo.InvariantCulture, out double cornerOffset) || cornerOffset < 0)
            {
                MessageBox.Show("离角距离必须是非负数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private async Task GenerateCoordinateTableAsync()
        {
            // 先在 UI 线程读取所有界面参数，避免在 MCT/后台线程访问 WPF 控件
            var layoutName = LayoutComboBox.SelectedItem?.ToString();
            var layerName = LayerComboBox.SelectedItem?.ToString();
            var uniqueField = UniqueFieldComboBox.SelectedItem?.ToString();
            var titleText = TitleTextBox.Text;
            var prefixStr = PrefixTextBox.Text;
            var xyDecimal = int.Parse(XYDecimalTextBox.Text);
            var generateEdge = GenerateEdgeCheckBox.IsChecked == true;
            var edgeDecimal = int.Parse(EdgeDecimalTextBox.Text);
            var pointColWidth = double.Parse(PointColWidthTextBox.Text, CultureInfo.InvariantCulture);
            var xyColWidth = double.Parse(XYColWidthTextBox.Text, CultureInfo.InvariantCulture);
            var edgeColWidth = double.Parse(EdgeColWidthTextBox.Text, CultureInfo.InvariantCulture);
            var rowHeight = double.Parse(RowHeightTextBox.Text, CultureInfo.InvariantCulture);
            var areaUnit = AreaUnitComboBox.SelectedItem?.ToString() ?? "平方米";
            var areaDecimalText = GetTextBoxText("AreaDecimalTextBox");
            var areaDecimal = int.Parse(areaDecimalText);
            var muDecimalText = GetTextBoxText("MuDecimalTextBox");
            var muDecimal = int.Parse(muDecimalText);
            var placementCornerCombo = FindName("PlacementCornerComboBox") as ComboBox;
            var placementCorner = (placementCornerCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "左下角";
            var cornerOffsetText2 = GetTextBoxText("CornerOffsetTextBox");
            var cornerOffset = double.Parse(cornerOffsetText2, CultureInfo.InvariantCulture);
            var rowsPerColumn = int.Parse(RowsPerColumnTextBox.Text);
            var surveyStyle = SwapXYCheckBox?.IsChecked == true;

            await QueuedTask.Run(async () =>
            {
                try
                {
                    var project = ArcGIS.Desktop.Core.Project.Current;
                    var layoutItem = project.GetItems<LayoutProjectItem>().FirstOrDefault(l => l.Name == layoutName);

                    if (layoutItem == null)
                    {
                        Application.Current.Dispatcher.Invoke(() => LogMessage($"找不到布局: {layoutName}"));
                        return;
                    }

                    var layout = layoutItem.GetLayout();
                    var mapView = MapView.Active;
                    var layer = mapView.Map.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .FirstOrDefault(l => l.Name == layerName);

                    if (layer == null)
                    {
                        Application.Current.Dispatcher.Invoke(() => LogMessage($"找不到图层: {layerName}"));
                        return;
                    }

                    try
                    {
                        // 处理面要素
                        using (var table = layer.GetTable())
                        {
                            int processedCount = 0;
                            RowCursor cursor = null;

                            // 优先按选择集处理；若无选择则处理全部
                            var selectionDict = mapView.Map.GetSelection()?.ToDictionary();
                            List<long> selectedOids = null;
                            if (selectionDict != null)
                            {
                                foreach (var kvp in selectionDict)
                                {
                                    if (kvp.Key is FeatureLayer fl && fl.Name == layerName)
                                    {
                                        selectedOids = kvp.Value;
                                        break;
                                    }
                                }
                            }

                            if (selectedOids != null && selectedOids.Count > 0)
                            {
                                var def = table.GetDefinition();
                                string oidField = def.GetObjectIDField();
                                // 构造 IN 子句（选择数量通常有限）
                                string inClause = string.Join(",", selectedOids);
                                var qf = new QueryFilter
                                {
                                    WhereClause = $"{oidField} IN ({inClause})"
                                };
                                cursor = table.Search(qf);
                            }
                            else
                            {
                                cursor = table.Search();
                            }

                            using (cursor)
                            {
                                while (cursor.MoveNext())
                                {
                                    using (var row = cursor.Current)
                                    {
                                        var geometry = row["SHAPE"] as Polygon;
                                        var uniqueValue = row[uniqueField]?.ToString();

                                        if (geometry != null && !string.IsNullOrEmpty(uniqueValue))
                                        {
                                            await ProcessPolygonAsync(layout, geometry, uniqueValue, titleText, prefixStr,
                                                xyDecimal, generateEdge, edgeDecimal, pointColWidth, xyColWidth,
                                                edgeColWidth, rowHeight, areaUnit, areaDecimal, muDecimal, rowsPerColumn,
                                                placementCorner, cornerOffset, surveyStyle);

                                            processedCount++;
                                            // 记录处理进度，稍后在UI线程显示
                                            var logMsg = $"已处理面要素: {uniqueValue} ({processedCount})";
                                            Application.Current.Dispatcher.BeginInvoke(() => LogMessage(logMsg));
                                            
                                            // 添加短暂延迟，让布局有时间刷新渲染
                                            await Task.Delay(100);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.BeginInvoke(() => LogMessage($"处理过程中发生错误: {ex.Message}"));
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.BeginInvoke(() => LogMessage($"生成坐标表时发生错误: {ex.Message}"));
                    throw;
                }
            });
        }

        private async Task ProcessPolygonAsync(Layout layout, Polygon geometry, string uniqueValue, 
            string titleText, string prefixStr, int xyDecimal, bool generateEdge, int edgeDecimal,
            double pointColWidth, double xyColWidth, double edgeColWidth, double rowHeight, 
            string areaUnit, int areaDecimal, int muDecimal, int rowsPerColumn,
            string placementCorner, double cornerOffset, bool surveyStyle)
        {
            try
            {
                // 提取坐标点
                var coordinates = ExtractCoordinates(geometry, xyDecimal);
                var edgeLengths = CalculateEdgeLengths(coordinates);
                
                // 计算面积
                var (areaFormatted, muAreaFormatted) = FormatArea(geometry.Area, areaUnit, areaDecimal, muDecimal);
                
                // 生成表格文本
                var tableData = GenerateTableData(coordinates, titleText, prefixStr, generateEdge, 
                    edgeLengths, edgeDecimal, rowsPerColumn, surveyStyle);
                
                // 在布局上创建表格元素
                await CreateTableElementsAsync(layout, tableData, uniqueValue, areaFormatted, muAreaFormatted,
                    areaUnit, pointColWidth, xyColWidth, edgeColWidth, rowHeight, generateEdge,
                    placementCorner, cornerOffset);
                    
                _ = Task.Run(() => LogMessage($"面要素 {uniqueValue} 的坐标表已生成"));
            }
            catch (Exception ex)
            {
                _ = Task.Run(() => LogMessage($"处理面要素 {uniqueValue} 时发生错误: {ex.Message}"));
            }
        }

        private List<(double X, double Y, string XStr, string YStr)> ExtractCoordinates(Polygon geometry, int decimalPlaces)
        {
            var coordinates = new List<(double X, double Y, string XStr, string YStr)>();
            
            var points = geometry.Points;
            foreach (var point in points)
            {
                coordinates.Add((
                    point.X, 
                    point.Y,
                    point.X.ToString($"F{decimalPlaces}"),
                    point.Y.ToString($"F{decimalPlaces}")
                ));
            }
            
            // 确保首尾闭合
            if (coordinates.Count > 0 && 
                (coordinates[0].X != coordinates[coordinates.Count - 1].X || 
                 coordinates[0].Y != coordinates[coordinates.Count - 1].Y))
            {
                var first = coordinates[0];
                coordinates.Add(first);
            }
            
            return coordinates;
        }

        private List<double> CalculateEdgeLengths(List<(double X, double Y, string XStr, string YStr)> coordinates)
        {
            var edgeLengths = new List<double>();
            
            for (int i = 0; i < coordinates.Count - 1; i++)
            {
                double dx = coordinates[i + 1].X - coordinates[i].X;
                double dy = coordinates[i + 1].Y - coordinates[i].Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                edgeLengths.Add(length);
            }
            
            return edgeLengths;
        }

        private (string areaFormatted, string muAreaFormatted) FormatArea(double area, string unitChoice, 
            int decimalPlaces, int muDecimalPlaces)
        {
            if (unitChoice == "平方米")
            {
                // 先按主单位保留小数（四舍五入），再据此换算亩
                double main = Math.Round(area, decimalPlaces, MidpointRounding.AwayFromZero);
                string areaFormatted = main.ToString($"F{decimalPlaces}");
                double muArea = main * 0.0015; // 1 平方米 = 0.0015 亩
                string muAreaFormatted = muArea.ToString($"F{muDecimalPlaces}");
                return (areaFormatted, muAreaFormatted);
            }
            else if (unitChoice == "公顷")
            {
                double hectares = area / 10000.0;
                double main = Math.Round(hectares, decimalPlaces, MidpointRounding.AwayFromZero);
                string areaFormatted = main.ToString($"F{decimalPlaces}");
                double muArea = main * 15.0; // 1 公顷 = 15 亩
                string muAreaFormatted = muArea.ToString($"F{muDecimalPlaces}");
                return (areaFormatted, muAreaFormatted);
            }

            // 兜底：直接用平方米
            double fallbackMain = Math.Round(area, decimalPlaces, MidpointRounding.AwayFromZero);
            return (fallbackMain.ToString($"F{decimalPlaces}"), (fallbackMain * 0.0015).ToString($"F{muDecimalPlaces}"));
        }

        private List<string> GenerateTableData(List<(double X, double Y, string XStr, string YStr)> coordinates,
            string titleText, string prefixStr, bool generateEdge, List<double> edgeLengths, 
            int edgeDecimal, int rowsPerColumn, bool surveyStyle)
        {
            var tableData = new List<string>();
            
            // 构造表头
            string headerInfo = generateEdge ? 
                $"{titleText}\n点号    X坐标    Y坐标    边长" : 
                $"{titleText}\n点号    X坐标    Y坐标";
            
            // 构造数据行
            var dataLines = new List<string>();
            for (int i = 0; i < coordinates.Count; i++)
            {
                int displayNo = (i == coordinates.Count - 1) ? 1 : i + 1;
                string pointName = $"{prefixStr}{displayNo}";
                if (generateEdge && i < edgeLengths.Count)
                {
                    string edgeStr = edgeLengths[i].ToString($"F{edgeDecimal}");
                    string xOut = surveyStyle ? coordinates[i].YStr : coordinates[i].XStr;
                    string yOut = surveyStyle ? coordinates[i].XStr : coordinates[i].YStr;
                    string dataLine = $"{pointName}    {xOut}    {yOut}    {edgeStr}";
                    dataLines.Add(dataLine);
                }
                else
                {
                    string xOut = surveyStyle ? coordinates[i].YStr : coordinates[i].XStr;
                    string yOut = surveyStyle ? coordinates[i].XStr : coordinates[i].YStr;
                    string dataLine = $"{pointName}    {xOut}    {yOut}";
                    dataLines.Add(dataLine);
                }
            }
            
            // 分割子表
            if (dataLines.Count <= rowsPerColumn)
            {
                string subtable = headerInfo + "\n" + string.Join("\n", dataLines);
                tableData.Add(subtable);
            }
            else
            {
                // 第一子表
                var firstChunk = dataLines.Take(rowsPerColumn);
                string firstSubtable = headerInfo + "\n" + string.Join("\n", firstChunk);
                tableData.Add(firstSubtable);
                
                // 后续子表
                int index = rowsPerColumn;
                while (index < dataLines.Count)
                {
                    var lines = new List<string> { headerInfo.Split('\n')[0], headerInfo.Split('\n')[1] };
                    lines.Add(dataLines[index - 1]); // 重复上一行
                    
                    var nextChunk = dataLines.Skip(index).Take(rowsPerColumn - 1);
                    lines.AddRange(nextChunk);
                    
                    string subtable = string.Join("\n", lines);
                    tableData.Add(subtable);
                    
                    index += rowsPerColumn - 1;
                }
            }
            
            return tableData;
        }

        private async Task CreateTableElementsAsync(Layout layout, List<string> tableData, string uniqueValue,
            string areaFormatted, string muAreaFormatted, string areaUnit, double pointColWidth, 
            double xyColWidth, double edgeColWidth, double rowHeight, bool generateEdge,
            string placementCorner, double cornerOffset)
        {
            // 使用BeginInvoke避免线程阻塞
            Application.Current.Dispatcher.BeginInvoke(() => 
                LogMessage($"正在为 {uniqueValue} 创建表格元素..."));

            try
            {
                if (QueuedTask.OnWorker)
                {
                    CreateTableElementsOnMCT(layout, tableData, uniqueValue, areaFormatted, muAreaFormatted, areaUnit,
                        pointColWidth, xyColWidth, edgeColWidth, rowHeight, generateEdge, placementCorner, cornerOffset);
                }
                else
                {
                    await QueuedTask.Run(() =>
                        CreateTableElementsOnMCT(layout, tableData, uniqueValue, areaFormatted, muAreaFormatted, areaUnit,
                            pointColWidth, xyColWidth, edgeColWidth, rowHeight, generateEdge, placementCorner, cornerOffset));
                }

                Application.Current.Dispatcher.BeginInvoke(() => 
                    LogMessage($"面要素 {uniqueValue} 的表格元素创建完成"));
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.BeginInvoke(() => 
                    LogMessage($"创建表格元素时发生错误: {ex.Message}"));
                throw;
            }
        }

        private void CreateTableElementsOnMCT(Layout layout, List<string> tableData, string uniqueValue,
            string areaFormatted, string muAreaFormatted, string areaUnit, double pointColWidth, 
            double xyColWidth, double edgeColWidth, double rowHeight, bool generateEdge,
            string placementCorner, double cornerOffset)
        {
            // 为元素名称添加GUID确保唯一性，避免多次生成时的命名冲突
            string timestamp = Guid.NewGuid().ToString("N");
            
            // 检测并获取模板元素
            var templateRect = layout.FindElement("XF_TX") as GraphicElement;
            var templateText = layout.FindElement("XF_WB") as GraphicElement;
            
            CIMPolygonSymbol rectSymbol = null;
            CIMTextSymbol textSymbol = null;
            
            if (templateRect != null)
            {
                var graphic = templateRect.GetGraphic();
                if (graphic is CIMPolygonGraphic polygonGraphic && polygonGraphic.Symbol != null)
                {
                    rectSymbol = polygonGraphic.Symbol.Symbol as CIMPolygonSymbol;
                }
                Application.Current.Dispatcher.BeginInvoke(() =>
                    LogMessage(rectSymbol != null ? "检测到图形模板 XF_TX，将使用模板样式" : "图形模板 XF_TX 格式不正确，使用默认样式"));
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                    LogMessage("未找到图形模板 XF_TX，使用默认样式"));
            }
            
            if (templateText != null)
            {
                var graphic = templateText.GetGraphic();
                if (graphic is CIMTextGraphic textGraphic && textGraphic.Symbol != null)
                {
                    textSymbol = textGraphic.Symbol.Symbol as CIMTextSymbol;
                }
                Application.Current.Dispatcher.BeginInvoke(() =>
                    LogMessage(textSymbol != null ? "检测到文本模板 XF_WB，将使用模板样式" : "文本模板 XF_WB 格式不正确，使用默认样式"));
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                    LogMessage("未找到文本模板 XF_WB，使用默认样式"));
            }
            
            // 计算表格总宽度（直接使用毫米）
            double tableWidth = generateEdge ? 
                (pointColWidth + 2 * xyColWidth + edgeColWidth) :
                (pointColWidth + 2 * xyColWidth);
            
            double tableRowHeight = rowHeight;

            // 调试日志：输出尺寸（毫米）
            var widthsStr = generateEdge 
                ? $"{pointColWidth:F2},{xyColWidth:F2},{xyColWidth:F2},{edgeColWidth:F2}"
                : $"{pointColWidth:F2},{xyColWidth:F2},{xyColWidth:F2}";
            Application.Current.Dispatcher.BeginInvoke(() =>
                LogMessage($"列宽(mm)={widthsStr}；行高(mm)={rowHeight:F2}；表宽(mm)={tableWidth:F2}"));

            // 先计算每个子表的有效行数（不含空行）。最后一个子表会额外增加面积行。
            var subtableLineCounts = new List<int>();
            for (int idx = 0; idx < tableData.Count; idx++)
            {
                var cnt = tableData[idx]
                    .Split('\n')
                    .Select(s => s.Trim())
                    .Count(s => !string.IsNullOrEmpty(s));
                if (idx == tableData.Count - 1)
                    cnt += 1; // 面积行
                subtableLineCounts.Add(cnt);
            }
            int maxLineCount = subtableLineCounts.Count > 0 ? subtableLineCounts.Max() : 0;

            // 获取地图框，计算目标位置
            var mapFrame = layout.Elements.OfType<MapFrame>().FirstOrDefault();
            double startX;
            double startY;
            
            if (mapFrame != null)
            {
                var mapBounds = mapFrame.GetBounds();
                // 根据角落设置计算起始位置
                switch (placementCorner)
                {
                    case "左下角":
                        startX = mapBounds.XMin + cornerOffset;
                        startY = mapBounds.YMin + cornerOffset + (maxLineCount * tableRowHeight);
                        break;
                    case "右下角":
                        startX = mapBounds.XMax - tableWidth - cornerOffset;
                        startY = mapBounds.YMin + cornerOffset + (maxLineCount * tableRowHeight);
                        break;
                    case "左上角":
                        startX = mapBounds.XMin + cornerOffset;
                        startY = mapBounds.YMax - cornerOffset;
                        break;
                    case "右上角":
                        startX = mapBounds.XMax - tableWidth - cornerOffset;
                        startY = mapBounds.YMax - cornerOffset;
                        break;
                    default:
                        startX = mapBounds.XMin + cornerOffset;
                        startY = mapBounds.YMin + cornerOffset + (maxLineCount * tableRowHeight);
                        break;
                }
                
                Application.Current.Dispatcher.BeginInvoke(() =>
                    LogMessage($"地图框: ({mapBounds.XMin:F2},{mapBounds.YMin:F2})-({mapBounds.XMax:F2},{mapBounds.YMax:F2}), 起始位置: ({startX:F2},{startY:F2}), 角落={placementCorner}"));
            }
            else
            {
                // 如果没有地图框，使用默认位置
                startX = 10;
                startY = 300;
                Application.Current.Dispatcher.BeginInvoke(() =>
                    LogMessage($"警告: 未找到地图框，使用默认位置 ({startX},{startY})"));
            }
            
            var mainGroupElements = new List<Element>();
            double currentStartX = startX;

            // 处理每个子表
            for (int tableIndex = 0; tableIndex < tableData.Count; tableIndex++)
            {
                var tableText = tableData[tableIndex];
                var lines = tableText.Split('\n');
                var subGroupElements = new List<Element>();
                // 记录本子表“边长”列的起始X，以及首个数据行的顶部Y，用于后续错行绘制
                double edgeColumnXStart = 0;
                double firstDataRowTopY = 0;
                bool dataRowTopYCaptured = false;
                var dataRowTokens = new List<string[]>();
                // 底部对齐：根据该子表行数与最大行数的差，向上“抬高”起始Y，保证各列底边一致
                int thisLineCount = lines.Select(s => s.Trim()).Count(s => !string.IsNullOrEmpty(s));
                if (tableIndex == tableData.Count - 1)
                    thisLineCount += 1; // 该列会添加面积行
                double currentY = startY - (maxLineCount - thisLineCount) * tableRowHeight;

                // 绘制表格的每一行
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (i == 0)
                    {
                        // 第一行：标题行（跨整行绘制）
                        var titleElements = LayoutElementHelper.CreateTableCellSync(
                            layout, $"Title_{tableIndex}_{i}_{timestamp}",
                            (currentStartX, currentY), (tableWidth, tableRowHeight), line, true, rectSymbol, textSymbol);
                        subGroupElements.AddRange(titleElements);

                        currentY -= tableRowHeight;
                    }
                    else if (i == 1)
                    {
                        // 第二行：各列标题
                        var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        var colWidths = generateEdge ? 
                            new[] { pointColWidth, xyColWidth, xyColWidth, edgeColWidth } :
                            new[] { pointColWidth, xyColWidth, xyColWidth };

                        double xOffset = currentStartX;
                        for (int j = 0; j < Math.Min(tokens.Length, colWidths.Length); j++)
                        {
                            double cellWidth = colWidths[j]; // mm
                            
                            var headerElements = LayoutElementHelper.CreateTableCellSync(
                                layout, $"Header_{tableIndex}_{i}_{j}_{timestamp}",
                                (xOffset, currentY), (cellWidth, tableRowHeight), tokens[j], true, rectSymbol, textSymbol);
                            subGroupElements.AddRange(headerElements);

                            xOffset += cellWidth;
                        }
                        // 计算边长列X起点
                        if (generateEdge)
                        {
                            edgeColumnXStart = currentStartX + pointColWidth + xyColWidth + xyColWidth;
                        }
                        // 紧接其后就是第一条数据行的顶部Y
                        firstDataRowTopY = currentY - tableRowHeight;
                        dataRowTopYCaptured = true;
                        currentY -= tableRowHeight;
                    }
                    else
                    {
                        // 数据行：根据是否生成边长决定列数
                        var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length < 3) continue;

                        var colWidths = generateEdge ?
                            new[] { pointColWidth, xyColWidth, xyColWidth, edgeColWidth } :
                            new[] { pointColWidth, xyColWidth, xyColWidth };
                        double xOffset = currentStartX;

                        for (int j = 0; j < Math.Min(tokens.Length, colWidths.Length); j++)
                        {
                            double cellWidth = colWidths[j]; // mm
                            // 若需要错行绘制边长列，则此处跳过边长列（在后续单独按半格错位绘制）
                            if (generateEdge && j == colWidths.Length - 1)
                            {
                                xOffset += cellWidth;
                                continue;
                            }

                            var dataElements = LayoutElementHelper.CreateTableCellSync(
                                layout, $"Data_{tableIndex}_{i}_{j}_{timestamp}",
                                (xOffset, currentY), (cellWidth, tableRowHeight), tokens[j], false, rectSymbol, textSymbol);
                            subGroupElements.AddRange(dataElements);

                            xOffset += cellWidth;
                        }
                        // 收集数据行tokens，后续用于边长列错行绘制
                        dataRowTokens.Add(tokens);
                        currentY -= tableRowHeight;
                    }
                }

                // 在数据行绘制完成后，若启用边长列，则按“错行+首尾补半格”的方式绘制边长列
                if (generateEdge && dataRowTopYCaptured && dataRowTokens.Count > 0)
                {
                    double xEdge = edgeColumnXStart;

                    // 顶部补半格（空白）
                    var padTop = LayoutElementHelper.CreateTableCellSync(
                        layout, $"EdgePadTop_{tableIndex}_{timestamp}",
                        (xEdge, firstDataRowTopY), (edgeColWidth, tableRowHeight / 2.0), string.Empty, false, rectSymbol, textSymbol);
                    subGroupElements.AddRange(padTop);

                    // 中间边长值，位于相邻两条数据行之间（以第一条数据行顶部向下0.5行起始）
                    for (int e = 0; e < dataRowTokens.Count - 1; e++)
                    {
                        string edgeText = dataRowTokens[e].Length > 3 ? dataRowTokens[e][3] : string.Empty;
                        double edgeTopY = firstDataRowTopY - (e + 0.5) * tableRowHeight;

                        var edgeElems = LayoutElementHelper.CreateTableCellSync(
                            layout, $"Edge_{tableIndex}_{e}_{timestamp}",
                            (xEdge, edgeTopY), (edgeColWidth, tableRowHeight), edgeText, false, rectSymbol, textSymbol);
                        subGroupElements.AddRange(edgeElems);
                    }

                    // 底部补半格（空白）
                    double bottomPadTop = firstDataRowTopY - (dataRowTokens.Count - 0.5) * tableRowHeight;
                    var padBottom = LayoutElementHelper.CreateTableCellSync(
                        layout, $"EdgePadBottom_{tableIndex}_{timestamp}",
                        (xEdge, bottomPadTop), (edgeColWidth, tableRowHeight / 2.0), string.Empty, false, rectSymbol, textSymbol);
                    subGroupElements.AddRange(padBottom);
                }

                // 如果是最后一个子表，添加面积信息
                if (tableIndex == tableData.Count - 1)
                {
                    string areaText = $"S={areaFormatted} {areaUnit} 合 {muAreaFormatted} 亩";
                    
                    var areaElements = LayoutElementHelper.CreateTableCellSync(
                        layout, $"Area_{tableIndex}_{timestamp}",
                        (currentStartX, currentY), (tableWidth, tableRowHeight), areaText, false, rectSymbol, textSymbol);
                    subGroupElements.AddRange(areaElements);
                }

                // 创建子组
                if (subGroupElements.Count > 0)
                {
                    string subGroupName = $"Group_{uniqueValue}_Sub_{tableIndex}_{timestamp}";
                    var subGroup = ElementFactory.Instance.CreateGroupElement(layout, subGroupElements, subGroupName);
                    if (subGroup != null) 
                    {
                        mainGroupElements.Add(subGroup);
                        // 每个子组创建后刷新布局视图
                        var lv = LayoutView.Active;
                        if (lv != null && lv.Layout == layout)
                        {
                            lv.Refresh();
                        }
                    }
                }

                // 调整下一个子表的起始X坐标
                if (tableIndex < tableData.Count - 1)
                {
                    // 右侧角落（右下角、右上角）：从右往左排列
                    // 左侧角落（左下角、左上角）：从左往右排列
                    if (placementCorner == "右下角" || placementCorner == "右上角")
                    {
                        currentStartX -= tableWidth;
                    }
                    else
                    {
                        currentStartX += tableWidth;
                    }
                }
            }

            // 创建主组（元素已经在正确位置创建，无需再移动）
            if (mainGroupElements.Count > 0)
            {
                string mainGroupName = $"Group_{uniqueValue}_{timestamp}";
                var mainGroup = ElementFactory.Instance.CreateGroupElement(layout, mainGroupElements, mainGroupName);
                
                // 刷新布局视图确保元素正确渲染
                var layoutView = LayoutView.Active;
                if (layoutView != null && layoutView.Layout == layout)
                {
                    layoutView.Refresh();
                }
                
                Application.Current.Dispatcher.BeginInvoke(() =>
                    LogMessage($"坐标表组创建完成: {mainGroupName}"));
            }
        }

        private void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}\r\n";
            
            if (LogTextBox.Dispatcher.CheckAccess())
            {
                LogTextBox.AppendText(logEntry);
                LogTextBox.ScrollToEnd();
            }
            else
            {
                LogTextBox.Dispatcher.Invoke(() =>
                {
                    LogTextBox.AppendText(logEntry);
                    LogTextBox.ScrollToEnd();
                });
            }
        }
    }
}
