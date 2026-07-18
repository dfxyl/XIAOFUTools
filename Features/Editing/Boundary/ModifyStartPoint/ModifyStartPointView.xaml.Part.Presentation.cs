using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace XIAOFUTools.Features.Editing.Boundary.ModifyStartPoint
{
    public partial class ModifyStartPointView
    {

        // 开始按钮点击事件处理
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedLayer = LayerComboBox.SelectedItem as FeatureLayer;
            var selectedCorner = CornerComboBox.SelectedItem as string;

            if (selectedLayer == null)
            {
                MessageBox.Show("请选择一个面图层", "提示");
                return;
            }

            if (selectedCorner == null)
            {
                MessageBox.Show("请选择起始角", "提示");
                return;
            }

            if (!double.TryParse(AngleThresholdTextBox.Text, out double angleThreshold) || angleThreshold < 0 || angleThreshold > 180)
            {
                MessageBox.Show("请输入有效的角度阈值（0-180）", "提示");
                return;
            }

            // 清空日志
            LogTextBox.Clear();
            LogInfo($"开始处理图层: {selectedLayer.Name}");
            LogInfo($"起始角: {selectedCorner}");
            LogInfo($"角度阈值: {angleThreshold}度");

            ProgressBar.Visibility = System.Windows.Visibility.Visible;
            ProgressBar.Value = 0;
            LogInfo("正在处理...");

            // 直接使用中文角点名称，确保与 GetStartPoint 中的判断一致
            ProcessPolygons(selectedLayer, selectedCorner, angleThreshold);
        }

        // 帮助按钮点击事件
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string helpText = @"修改起始点工具使用说明：

功能描述：
批量修改面要素的起始点位置，可根据指定的角（西北角、东北角、东南角、西南角）和角度阈值来确定新的起始点。

参数说明：
• 选择面图层：要处理的面要素图层
• 起始角：指定起始点应该位于的角位置
• 内角阈值：用于筛选有效角点的阈值（默认179度）

操作步骤：
1. 选择要处理的面图层
2. 选择起始角位置
3. 设置角度阈值（可选）
4. 点击执行按钮开始处理

注意事项：
• 处理前请确保已保存编辑
• 处理完成后需要刷新地图查看结果
• 角度阈值越小，筛选条件越严格";

            MessageBox.Show(helpText, "帮助信息", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        // 刷新图层列表（图层选择行右侧按钮）
        private void RefreshLayersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var prevSelected = LayerComboBox.SelectedItem as FeatureLayer;
                // 重新加载图层
                LoadPolygonLayers();

                // 尝试按名称恢复之前的选择
                if (prevSelected != null && LayerComboBox.ItemsSource is IEnumerable<FeatureLayer> layers)
                {
                    var match = layers.FirstOrDefault(l => l.Name == prevSelected.Name);
                    if (match != null)
                        LayerComboBox.SelectedItem = match;
                }

                LogInfo("图层列表已刷新。");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshLayersButton Error: {ex.Message}");
                MessageBox.Show($"刷新图层列表失败：{ex.Message}", "错误");
            }
        }

        // 日志记录方法
        private void LogInfo(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogTextBox.AppendText($"[{timestamp}] {message}\n");
            LogTextBox.ScrollToEnd();
        }

        private void LogError(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogTextBox.AppendText($"[{timestamp}] [错误] {message}\n");
            LogTextBox.ScrollToEnd();
        }

        private void LogWarning(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogTextBox.AppendText($"[{timestamp}] [警告] {message}\n");
            LogTextBox.ScrollToEnd();
        }

        // 处理多边形
        private async void ProcessPolygons(FeatureLayer layer, string cornerTag, double angleThreshold)
        {
            try
            {
                int totalCount = 0;
                int processedCount = 0;
                
                await QueuedTask.Run(() =>
                {
                    var editOperation = new EditOperation();
                    editOperation.Name = "修改多边形起始点";
                    editOperation.ProgressMessage = "正在处理...";
                    editOperation.ShowProgressor = true;

                    var featureClass = layer.GetFeatureClass();
                    
                    using (var cursor = featureClass.Search())
                    {
                        while (cursor.MoveNext())
                        {
                            totalCount++;
                        }
                    }
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        LogInfo($"共找到 {totalCount} 个面要素");
                    });

                    using (var cursor = featureClass.Search())
                    {
                        while (cursor.MoveNext())
                        {
                            using (var feature = cursor.Current as Feature)
                            {
                                var geometry = feature.GetShape() as Polygon;
                                if (geometry != null)
                                {
                                    var resultPolygon = ReshotMapPointReturnPolygon(geometry, cornerTag, angleThreshold);
                                    if (resultPolygon != null)
                                    {
                                        // 使用 EditOperation 的几何修改重载，确保正确更新 Shape 字段
                                        editOperation.Modify(layer, feature.GetObjectID(), resultPolygon);
                                    }
                                }
                            }

                            processedCount++;
                            double progress = (double)processedCount / totalCount * 100;
                            
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ProgressBar.Value = progress;
                                
                                if (processedCount % 10 == 0 || processedCount == totalCount)
                                {
                                    LogInfo($"已处理 {processedCount}/{totalCount} 个要素");
                                }
                            });
                        }
                    }

                    bool result = editOperation.Execute();
                    if (!result)
                    {
                        throw new Exception("编辑操作执行失败");
                    }
                });

                // 状态显示改为日志输出
                LogInfo($"处理完成！共成功处理 {processedCount} 个要素");
                MessageBox.Show($"处理完成，共处理 {processedCount} 个要素", "完成");
            }
            catch (Exception ex)
            {
                // 状态显示改为日志输出
                LogError("处理失败");
                LogError($"处理失败：{ex.Message}");
                MessageBox.Show($"处理过程中发生错误：{ex.Message}", "错误");
            }
            finally
            {
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        // 重新排序多边形点
        private Polygon ReshotMapPointReturnPolygon(Polygon polygon, string corner, double angleThreshold)
        {
            try
            {
                if (polygon?.Parts?.Count == 0) return polygon;

                var polygonBuilder = new PolygonBuilderEx(polygon.SpatialReference);

                for (int i = 0; i < polygon.PartCount; i++)
                {
                    var part = polygon.Parts[i];
                    var points = ExtractPointsFromSegments(part);
                    var reorderedPoints = ReorderRingPoints(points, i == 0, corner, angleThreshold);
                    if (reorderedPoints?.Count > 0)
                    {
                        polygonBuilder.AddPart(reorderedPoints);
                    }
                }

                return polygonBuilder.ToGeometry();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReshotMapPointReturnPolygon Error: {ex.Message}");
                return polygon;
            }
        }
    }
}
