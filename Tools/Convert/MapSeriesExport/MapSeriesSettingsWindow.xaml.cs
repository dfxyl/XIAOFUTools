using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Tools.Edit.Boundary.LayoutCoordinateTable;

namespace XIAOFUTools.Tools.Output.MapSeriesExport
{
    /// <summary>
    /// 驱动制图设置窗口
    /// </summary>
    public partial class MapSeriesSettingsWindow : Window
    {
        /// <summary>
        /// 坐标表设置
        /// </summary>
        public CoordinateTableSettings CoordinateTableSettings { get; private set; }
        public event Action<CoordinateTableSettings> SettingsSaved;
        
        private Layout _currentLayout;

        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIAOFUTools", "MapSeriesSettings.json");

        public MapSeriesSettingsWindow(CoordinateTableSettings existingSettings = null)
        {
            InitializeComponent();
            InitializeAreaUnits();
            LoadMapFramesAsync();
            
            if (existingSettings != null)
            {
                LoadSettings(existingSettings);
            }
            else
            {
                // 尝试从文件加载上次保存的设置
                var savedSettings = LoadSettingsFromFile();
                if (savedSettings != null)
                {
                    LoadSettings(savedSettings);
                }
            }
        }

        private void InitializeAreaUnits()
        {
            AreaUnitComboBox.Items.Clear();
            AreaUnitComboBox.Items.Add("平方米");
            AreaUnitComboBox.Items.Add("公顷");
            AreaUnitComboBox.SelectedIndex = 0;
        }
        
        private async void LoadMapFramesAsync()
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var layoutView = LayoutView.Active;
                    if (layoutView == null) return;
                    
                    _currentLayout = layoutView.Layout;
                    if (_currentLayout == null) return;
                    
                    var mapFrames = _currentLayout.Elements.OfType<MapFrame>().Select(mf => mf.Name).ToList();
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MapFrameComboBox.Items.Clear();
                        foreach (var name in mapFrames)
                        {
                            MapFrameComboBox.Items.Add(name);
                        }
                        if (MapFrameComboBox.Items.Count > 0)
                        {
                            MapFrameComboBox.SelectedIndex = 0;
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载地图框失败: {ex.Message}");
            }
        }
        
        private void RefreshMapFrames_Click(object sender, RoutedEventArgs e)
        {
            LoadMapFramesAsync();
        }
        
        private void PositionModeChanged(object sender, RoutedEventArgs e)
        {
            if (MapFramePanel == null || AnchorPanel == null) return;
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
        
        private void AreaModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AutoAreaPanel == null || CustomAreaPanel == null) return;
            
            var selectedItem = AreaModeComboBox.SelectedItem as ComboBoxItem;
            var mode = selectedItem?.Content?.ToString() ?? "自动生成";
            
            switch (mode)
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
        
        private async void CreateAnchor_Click(object sender, RoutedEventArgs e)
        {
            var anchorName = AnchorNameTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(anchorName))
            {
                MessageBox.Show("请输入锚点名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                await QueuedTask.Run(() =>
                {
                    var layoutView = LayoutView.Active;
                    if (layoutView == null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show("请先打开布局视图", "提示", MessageBoxButton.OK, MessageBoxImage.Warning));
                        return;
                    }
                    
                    var layout = layoutView.Layout;
                    
                    // 检查是否已存在同名元素
                    var existing = layout.FindElement(anchorName);
                    if (existing != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show($"布局中已存在名为 '{anchorName}' 的元素", "提示", MessageBoxButton.OK, MessageBoxImage.Information));
                        return;
                    }
                    
                    // 在布局中心创建一个小圆点作为锚点
                    var pageWidth = layout.GetPage().Width;
                    var pageHeight = layout.GetPage().Height;
                    var centerX = pageWidth / 2;
                    var centerY = pageHeight / 2;
                    
                    // 创建点
                    var point = MapPointBuilderEx.CreateMapPoint(centerX, centerY);
                    
                    // 创建点符号
                    var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(
                        ColorFactory.Instance.RedRGB, 4, SimpleMarkerStyle.Circle);
                    
                    // 创建点元素
                    var pointElement = ElementFactory.Instance.CreateGraphicElement(
                        layout, point, pointSymbol, anchorName);
                    
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show($"锚点 '{anchorName}' 创建成功！\n请在布局中将其移动到所需位置。", "成功", MessageBoxButton.OK, MessageBoxImage.Information));
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建锚点失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void CreateTemplates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var layoutView = LayoutView.Active;
                    if (layoutView == null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show("请先打开布局视图", "提示", MessageBoxButton.OK, MessageBoxImage.Warning));
                        return;
                    }
                    
                    var layout = layoutView.Layout;
                    bool txCreated = false, wbCreated = false;
                    
                    // 查找并创建 XF_TX（图形模板）
                    var tx = layout.FindElement("XF_TX");
                    if (tx == null)
                    {
                        LayoutElementHelper.CreateRectangleTemplateSync(
                            layout, name: "XF_TX", position: (-30, -30), size: (50, 20), symbol: null);
                        txCreated = true;
                    }
                    
                    // 查找并创建 XF_WB（文本模板）
                    var wb = layout.FindElement("XF_WB");
                    if (wb == null)
                    {
                        LayoutElementHelper.CreateTextTemplateSync(
                            layout, name: "XF_WB", position: (-30, -55), text: "模板文本", symbol: null);
                        wbCreated = true;
                    }
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (txCreated || wbCreated)
                        {
                            string msg = "已创建模板：";
                            if (txCreated) msg += "\n• XF_TX（图形模板）";
                            if (wbCreated) msg += "\n• XF_WB（文本模板）";
                            msg += "\n\n模板位于版面外（左下角负坐标），可设置颜色、字体等样式后生成时将复制使用。";
                            MessageBox.Show(msg, "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("模板已存在（XF_TX、XF_WB），无需重复创建。\n可直接修改模板样式。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建模板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
            AreaDecimalTextBox.Text = settings.AreaDecimal.ToString();
            MuDecimalTextBox.Text = settings.MuDecimal.ToString();
            
            // 定位方式
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
            
            // 地图框
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
            
            // 锚点名称
            AnchorNameTextBox.Text = settings.AnchorElementName ?? "XF_MD";
            
            // 设置放置位置
            foreach (ComboBoxItem item in PlacementCornerComboBox.Items)
            {
                if (item.Content?.ToString() == settings.PlacementCorner)
                {
                    PlacementCornerComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // 面积模式
            foreach (ComboBoxItem item in AreaModeComboBox.Items)
            {
                if (item.Content?.ToString() == settings.AreaMode)
                {
                    AreaModeComboBox.SelectedItem = item;
                    break;
                }
            }
            CustomAreaTextBox.Text = settings.CustomAreaText ?? "S=[面积] 平方米";
            ExportDelayTextBox.Text = settings.ExportDelay.ToString();
            
            // 界址点设置
            EnableBoundaryPointsCheckBox.IsChecked = settings.EnableBoundaryPoints;
            BoundaryPointSizeTextBox.Text = settings.BoundaryPointSize.ToString(CultureInfo.InvariantCulture);
            
            // 点号设置
            EnablePointLabelsCheckBox.IsChecked = settings.EnablePointLabels;
            PointLabelPrefixTextBox.Text = settings.PointLabelPrefix ?? "J";
            PointLabelSuffixTextBox.Text = settings.PointLabelSuffix ?? "";
            PointLabelDistanceTextBox.Text = settings.PointLabelDistance.ToString(CultureInfo.InvariantCulture);
            PointLabelSizeTextBox.Text = settings.PointLabelSize.ToString(CultureInfo.InvariantCulture);
            
            // 压盖处理
            foreach (ComboBoxItem item in PointLabelOverlapComboBox.Items)
            {
                if (item.Content?.ToString() == settings.PointLabelOverlapMode)
                {
                    PointLabelOverlapComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // 边长标注设置
            EnableEdgeLabelsCheckBox.IsChecked = settings.EnableEdgeLabels;
            EdgeLabelPrefixTextBox.Text = settings.EdgeLabelPrefix ?? "";
            EdgeLabelSuffixTextBox.Text = settings.EdgeLabelSuffix ?? "";
            EdgeLabelDecimalTextBox.Text = settings.EdgeLabelDecimal.ToString();
            EdgeLabelPadZerosCheckBox.IsChecked = settings.EdgeLabelPadZeros;
            EdgeLabelDistanceTextBox.Text = settings.EdgeLabelDistance.ToString(CultureInfo.InvariantCulture);
            EdgeLabelSizeTextBox.Text = settings.EdgeLabelSize.ToString(CultureInfo.InvariantCulture);
            
            // 边长压盖处理
            foreach (ComboBoxItem item in EdgeLabelOverlapComboBox.Items)
            {
                if (item.Content?.ToString() == settings.EdgeLabelOverlapMode)
                {
                    EdgeLabelOverlapComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // 设置面积单位
            for (int i = 0; i < AreaUnitComboBox.Items.Count; i++)
            {
                if (AreaUnitComboBox.Items[i].ToString() == settings.AreaUnit)
                {
                    AreaUnitComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs()) return;

            CoordinateTableSettings = new CoordinateTableSettings
            {
                EnableCoordinateTable = EnableCoordinateTableCheckBox.IsChecked == true,
                // 图层和字段从地图系列自动获取，不需要手动设置
                LayerName = null,
                UniqueField = null,
                Title = TitleTextBox.Text,
                Prefix = PrefixTextBox.Text,
                DecimalPlaces = int.Parse(DecimalTextBox.Text),
                GenerateEdge = GenerateEdgeCheckBox.IsChecked == true,
                // 定位设置
                UseAnchorPosition = AnchorPositionRadio.IsChecked == true,
                MapFrameName = MapFrameComboBox.SelectedItem?.ToString() ?? "",
                AnchorElementName = AnchorNameTextBox.Text?.Trim() ?? "XF_MD",
                // 尺寸设置
                PointColWidth = double.Parse(PointColWidthTextBox.Text, CultureInfo.InvariantCulture),
                XYColWidth = double.Parse(XYColWidthTextBox.Text, CultureInfo.InvariantCulture),
                EdgeColWidth = double.Parse(EdgeColWidthTextBox.Text, CultureInfo.InvariantCulture),
                RowHeight = double.Parse(RowHeightTextBox.Text, CultureInfo.InvariantCulture),
                PlacementCorner = (PlacementCornerComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "左下角",
                CornerOffset = double.Parse(CornerOffsetTextBox.Text, CultureInfo.InvariantCulture),
                // 面积设置
                AreaMode = (AreaModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "自动生成",
                CustomAreaText = CustomAreaTextBox.Text ?? "S=[面积] 平方米",
                AreaUnit = AreaUnitComboBox.SelectedItem?.ToString() ?? "平方米",
                AreaDecimal = int.Parse(AreaDecimalTextBox.Text),
                MuDecimal = int.Parse(MuDecimalTextBox.Text),
                RowsPerColumn = int.Parse(RowsPerColumnTextBox.Text),
                ExportDelay = int.Parse(ExportDelayTextBox.Text),
                // 界址点设置
                EnableBoundaryPoints = EnableBoundaryPointsCheckBox.IsChecked == true,
                BoundaryPointSize = double.Parse(BoundaryPointSizeTextBox.Text, CultureInfo.InvariantCulture),
                // 点号设置
                EnablePointLabels = EnablePointLabelsCheckBox.IsChecked == true,
                PointLabelPrefix = PointLabelPrefixTextBox.Text ?? "J",
                PointLabelSuffix = PointLabelSuffixTextBox.Text ?? "",
                PointLabelDistance = double.Parse(PointLabelDistanceTextBox.Text, CultureInfo.InvariantCulture),
                PointLabelSize = double.Parse(PointLabelSizeTextBox.Text, CultureInfo.InvariantCulture),
                PointLabelOverlapMode = (PointLabelOverlapComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "压盖隐藏",
                // 边长标注设置
                EnableEdgeLabels = EnableEdgeLabelsCheckBox.IsChecked == true,
                EdgeLabelPrefix = EdgeLabelPrefixTextBox.Text ?? "",
                EdgeLabelSuffix = EdgeLabelSuffixTextBox.Text ?? "",
                EdgeLabelDecimal = int.Parse(EdgeLabelDecimalTextBox.Text),
                EdgeLabelPadZeros = EdgeLabelPadZerosCheckBox.IsChecked == true,
                EdgeLabelDistance = double.Parse(EdgeLabelDistanceTextBox.Text, CultureInfo.InvariantCulture),
                EdgeLabelSize = double.Parse(EdgeLabelSizeTextBox.Text, CultureInfo.InvariantCulture),
                EdgeLabelOverlapMode = (EdgeLabelOverlapComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "压盖隐藏"
            };

            // 保存设置到文件
            SaveSettingsToFile(CoordinateTableSettings);
            
            SettingsSaved?.Invoke(CoordinateTableSettings);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 从文件加载设置
        /// </summary>
        private CoordinateTableSettings LoadSettingsFromFile()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<CoordinateTableSettings>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 保存设置到文件
        /// </summary>
        private void SaveSettingsToFile(CoordinateTableSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }

        private bool ValidateInputs()
        {
            if (!int.TryParse(DecimalTextBox.Text, out int dec) || dec < 0 || dec > 10)
            {
                MessageBox.Show("小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!double.TryParse(PointColWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double pw) || pw <= 0)
            {
                MessageBox.Show("点号列宽必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!double.TryParse(XYColWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double xyw) || xyw <= 0)
            {
                MessageBox.Show("坐标列宽必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!double.TryParse(EdgeColWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double ew) || ew <= 0)
            {
                MessageBox.Show("边长列宽必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!double.TryParse(RowHeightTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double rh) || rh <= 0)
            {
                MessageBox.Show("行高必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!double.TryParse(CornerOffsetTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double co) || co < 0)
            {
                MessageBox.Show("偏移量必须是非负数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(RowsPerColumnTextBox.Text, out int rpc) || rpc < 1)
            {
                MessageBox.Show("每列行数必须是正整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(ExportDelayTextBox.Text, out int delay) || delay < 0 || delay > 10000)
            {
                MessageBox.Show("等待时间必须是0-10000之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(AreaDecimalTextBox.Text, out int ad) || ad < 0 || ad > 10)
            {
                MessageBox.Show("面积小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(MuDecimalTextBox.Text, out int md) || md < 0 || md > 10)
            {
                MessageBox.Show("亩位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            if (!double.TryParse(BoundaryPointSizeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double bps) || bps <= 0)
            {
                MessageBox.Show("界址点大小必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            if (!double.TryParse(PointLabelDistanceTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double pld) || pld < 0)
            {
                MessageBox.Show("点号距离必须是非负数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            if (!double.TryParse(PointLabelSizeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double pls) || pls <= 0)
            {
                MessageBox.Show("点号大小必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            // 边长标注验证
            if (!int.TryParse(EdgeLabelDecimalTextBox.Text, out int eld) || eld < 0 || eld > 10)
            {
                MessageBox.Show("边长小数位数必须是0-10之间的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            if (!double.TryParse(EdgeLabelDistanceTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double eldi) || eldi < 0)
            {
                MessageBox.Show("边长距离必须是非负数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            if (!double.TryParse(EdgeLabelSizeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double els) || els <= 0)
            {
                MessageBox.Show("边长字体大小必须是正数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
        
        /// <summary>
        /// 创建界址点模板
        /// </summary>
        private async void CreatePointTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var layoutView = LayoutView.Active;
                    if (layoutView == null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show("请先打开布局视图", "提示", MessageBoxButton.OK, MessageBoxImage.Warning));
                        return;
                    }
                    
                    var layout = layoutView.Layout;
                    
                    // 查找并创建 XF_JZD（界址点模板）
                    var jzd = layout.FindElement("XF_JZD");
                    if (jzd == null)
                    {
                        // 创建红色圆点模板
                        var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                        var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(redColor, 8, SimpleMarkerStyle.Circle);
                        
                        var templatePoint = MapPointBuilderEx.CreateMapPoint(-35, -35);
                        var pointGraphic = new CIMPointGraphic
                        {
                            Location = templatePoint,
                            Symbol = pointSymbol.MakeSymbolReference()
                        };
                        
                        var graphicInfo = new ElementInfo { CustomProperties = new List<CIMStringMap>() };
                        var pointElement = ElementFactory.Instance.CreateGraphicElement(layout, pointGraphic, "XF_JZD", true, graphicInfo);
                        
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show("已创建界址点模板（XF_JZD）\n位于版面外（左下角负坐标），可设置颜色、大小等样式。", "成功", MessageBoxButton.OK, MessageBoxImage.Information));
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show("界址点模板已存在（XF_JZD），无需重复创建。\n可直接修改模板样式。", "提示", MessageBoxButton.OK, MessageBoxImage.Information));
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建界址点模板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 创建边长文本模板
        /// </summary>
        private async void CreateEdgeLabelTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var layoutView = LayoutView.Active;
                    if (layoutView == null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show("请先打开布局视图", "提示", MessageBoxButton.OK, MessageBoxImage.Warning));
                        return;
                    }
                    
                    var layout = layoutView.Layout;
                    
                    // 查找并创建 XF_BC（边长模板）
                    var bc = layout.FindElement("XF_BC");
                    if (bc == null)
                    {
                        // 创建红色文本模板
                        var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                        var textSymbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, 10, "Arial", "Regular");
                        
                        var templatePoint = MapPointBuilderEx.CreateMapPoint(-35, -50);
                        var textGraphic = new CIMTextGraphic
                        {
                            Shape = templatePoint,
                            Text = "12.34",
                            Symbol = textSymbol.MakeSymbolReference()
                        };
                        
                        var graphicInfo = new ElementInfo { CustomProperties = new List<CIMStringMap>() };
                        var textElement = ElementFactory.Instance.CreateGraphicElement(layout, textGraphic, "XF_BC", true, graphicInfo);
                        
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show("已创建边长文本模板（XF_BC）\n位于版面外（左下角负坐标），可设置字体、颜色等样式。", "成功", MessageBoxButton.OK, MessageBoxImage.Information));
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show("边长文本模板已存在（XF_BC），无需重复创建。\n可直接修改模板样式。", "提示", MessageBoxButton.OK, MessageBoxImage.Information));
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建边长模板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 创建点号文本模板
        /// </summary>
        private async void CreateLabelTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var layoutView = LayoutView.Active;
                    if (layoutView == null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show("请先打开布局视图", "提示", MessageBoxButton.OK, MessageBoxImage.Warning));
                        return;
                    }
                    
                    var layout = layoutView.Layout;
                    
                    // 查找并创建 XF_DH（点号模板）
                    var dh = layout.FindElement("XF_DH");
                    if (dh == null)
                    {
                        // 创建红色文本模板
                        var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                        var textSymbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, 12, "Arial", "Regular");
                        
                        var templatePoint = MapPointBuilderEx.CreateMapPoint(-35, -40);
                        var textGraphic = new CIMTextGraphic
                        {
                            Shape = templatePoint,
                            Text = "J1",
                            Symbol = textSymbol.MakeSymbolReference()
                        };
                        
                        var graphicInfo = new ElementInfo { CustomProperties = new List<CIMStringMap>() };
                        var textElement = ElementFactory.Instance.CreateGraphicElement(layout, textGraphic, "XF_DH", true, graphicInfo);
                        
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show("已创建点号文本模板（XF_DH）\n位于版面外（左下角负坐标），可设置字体、颜色等样式。", "成功", MessageBoxButton.OK, MessageBoxImage.Information));
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show("点号文本模板已存在（XF_DH），无需重复创建。\n可直接修改模板样式。", "提示", MessageBoxButton.OK, MessageBoxImage.Information));
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建点号模板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// 坐标表设置数据类
    /// </summary>
    public class CoordinateTableSettings
    {
        public bool EnableCoordinateTable { get; set; } = false;
        public string LayerName { get; set; } = "";
        public string UniqueField { get; set; } = "";
        public string TitleText { get; set; } = "2000国家大地坐标系111°";
        public string PointPrefix { get; set; } = "J";
        public int XYDecimal { get; set; } = 3;
        public bool GenerateEdge { get; set; } = true;
        public int EdgeDecimal { get; set; } = 2;
        
        // 定位设置
        public bool UseAnchorPosition { get; set; } = false;  // false=地图框定位, true=锚点定位
        public string MapFrameName { get; set; } = "";        // 地图框名称
        public string AnchorElementName { get; set; } = "XF_MD";  // 锚点元素名称
        
        // 尺寸设置
        public double PointColWidth { get; set; } = 10;
        public double XYColWidth { get; set; } = 22;
        public double EdgeColWidth { get; set; } = 12;
        public double RowHeight { get; set; } = 5.5;
        public string PlacementCorner { get; set; } = "左下角";
        public double CornerOffset { get; set; } = 2;
        
        // 面积设置
        public string AreaMode { get; set; } = "自动生成";  // 不生成、自动生成、自定义
        public string CustomAreaText { get; set; } = "S=[Shape_Area] 平方米";  // 自定义文本，[字段名]为占位符
        public string AreaUnit { get; set; } = "平方米";
        public int AreaDecimal { get; set; } = 2;
        public int MuDecimal { get; set; } = 4;
        public int RowsPerColumn { get; set; } = 20;
        public bool SwapXY { get; set; } = true;
        public int ExportDelay { get; set; } = 1000;  // 导出前等待时间（毫秒）
        
        // 界址点设置
        public bool EnableBoundaryPoints { get; set; } = false;  // 是否生成界址点
        public double BoundaryPointSize { get; set; } = 8.0;     // 点符号大小（点）
        
        // 点号设置
        public bool EnablePointLabels { get; set; } = false;     // 是否生成点号
        public string PointLabelPrefix { get; set; } = "J";      // 点号前缀
        public string PointLabelSuffix { get; set; } = "";       // 点号后缀
        public double PointLabelDistance { get; set; } = 3.0;    // 点号离点距离（毫米）
        public double PointLabelSize { get; set; } = 12.0;       // 点号字体大小（点）
        public string PointLabelOverlapMode { get; set; } = "压盖隐藏"; // 压盖模式：压盖隐藏、压盖避让
        
        // 边长标注设置
        public bool EnableEdgeLabels { get; set; } = false;      // 是否生成边长标注
        public string EdgeLabelPrefix { get; set; } = "";        // 边长前缀
        public string EdgeLabelSuffix { get; set; } = "";        // 边长后缀
        public int EdgeLabelDecimal { get; set; } = 2;           // 边长小数位数
        public bool EdgeLabelPadZeros { get; set; } = false;     // 边长补零（整数部分补到小数位数）
        public double EdgeLabelDistance { get; set; } = 2.0;     // 边长离边距离（毫米）
        public double EdgeLabelSize { get; set; } = 10.0;        // 边长字体大小（点）
        public string EdgeLabelOverlapMode { get; set; } = "压盖隐藏"; // 边长压盖模式
        
        // 别名属性（用于设置窗口兼容）
        public string Title { get => TitleText; set => TitleText = value; }
        public string Prefix { get => PointPrefix; set => PointPrefix = value; }
        public int DecimalPlaces { get => XYDecimal; set => XYDecimal = value; }
        
        /// <summary>
        /// 格式化点号文本
        /// </summary>
        public string FormatPointLabel(int index)
        {
            return $"{PointLabelPrefix}{index}{PointLabelSuffix}";
        }
        
        /// <summary>
        /// 格式化边长文本
        /// </summary>
        public string FormatEdgeLabel(double length)
        {
            string numStr;
            if (EdgeLabelPadZeros)
            {
                // 勾选补零时，小数部分补零到指定位数（如2.3 → 2.30）
                numStr = length.ToString($"F{EdgeLabelDecimal}");
            }
            else
            {
                // 不补零时，去掉尾部多余的零（如2.30 → 2.3）
                numStr = Math.Round(length, EdgeLabelDecimal).ToString();
            }
            return $"{EdgeLabelPrefix}{numStr}{EdgeLabelSuffix}";
        }
    }
}
