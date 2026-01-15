using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace XIAOFUTools.Tools.ViewArea
{
    /// <summary>
    /// ViewAreaDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class ViewAreaDockPaneView : UserControl
    {
        private ViewAreaDockPaneViewModel _viewModel;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ViewAreaDockPaneView()
        {
            InitializeComponent();
            _viewModel = new ViewAreaDockPaneViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// 当控件加载时刷新计算
        /// </summary>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.RefreshCommand?.Execute(null);
        }

        /// <summary>
        /// DataGrid单元格双击事件处理 - 复制数值（带重试机制）
        /// </summary>
        private async void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem != null)
            {
                var selectedCell = dataGrid.CurrentCell;
                if (selectedCell.Column != null && selectedCell.Item != null)
                {
                    // 获取单元格的值
                    var cellValue = selectedCell.Column.GetCellContent(selectedCell.Item);
                    if (cellValue is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text))
                    {
                        var success = await CopyToClipboardWithRetry(textBlock.Text);

                        if (success)
                        {
                            // 成功复制，显示短暂的视觉反馈
                            ShowCopySuccessVisualFeedback(textBlock);
                        }
                        else
                        {
                            // 复制失败，显示提示
                            ShowCopyFailureTooltip(dataGrid, "剪贴板被占用，请稍后重试");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 带重试机制的剪贴板复制（单个数值）
        /// </summary>
        private async Task<bool> CopyToClipboardWithRetry(string text, int maxRetries = 2)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        Clipboard.Clear();
                        System.Threading.Thread.Sleep(30); // 短暂等待
                        Clipboard.SetText(text);
                    });

                    return true; // 成功
                }
                catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x800401D0))
                {
                    // CLIPBRD_E_CANT_OPEN 错误，剪贴板被占用
                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(100); // 等待后重试
                    }
                }
                catch
                {
                    break; // 其他错误不重试
                }
            }

            return false; // 失败
        }

        /// <summary>
        /// 显示复制成功的视觉反馈
        /// </summary>
        private async void ShowCopySuccessVisualFeedback(TextBlock textBlock)
        {
            try
            {
                var originalBackground = textBlock.Background;
                var originalForeground = textBlock.Foreground;

                // 短暂高亮显示
                textBlock.Background = new SolidColorBrush(Color.FromArgb(100, 0, 120, 212)); // 半透明蓝色
                textBlock.Foreground = new SolidColorBrush(Colors.White);

                // 500毫秒后恢复原样
                await Task.Delay(500);

                textBlock.Background = originalBackground;
                textBlock.Foreground = originalForeground;
            }
            catch
            {
                // 忽略视觉反馈错误
            }
        }

        /// <summary>
        /// 显示复制失败的提示
        /// </summary>
        private void ShowCopyFailureTooltip(FrameworkElement element, string message)
        {
            try
            {
                var tooltip = new ToolTip
                {
                    Content = message,
                    IsOpen = true,
                    StaysOpen = false
                };

                element.ToolTip = tooltip;

                // 2秒后自动关闭
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, e) =>
                {
                    tooltip.IsOpen = false;
                    element.ToolTip = null;
                    timer.Stop();
                };
                timer.Start();
            }
            catch
            {
                // 忽略提示显示错误
            }
        }
    }
}
