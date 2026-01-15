using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.About
{
    /// <summary>
    /// AboutDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            LoadVersionInfo();
        }

        /// <summary>
        /// 加载版本信息
        /// </summary>
        private void LoadVersionInfo()
        {
            try
            {
                // 使用手动指定的版本号
                string versionString = XIAOFUTools.Common.VersionInfo.CurrentVersion;
                VersionText.Text = $"版本 {versionString}";
                CurrentVersionText.Text = versionString;

            }
            catch (Exception ex)
            {
                // 错误处理
                VersionText.Text = "版本信息获取失败";
                CurrentVersionText.Text = "未知";
                System.Diagnostics.Debug.WriteLine($"加载版本信息失败: {ex.Message}");
            }
        }

        

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// 窗口加载完成事件
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 可以在这里添加额外的初始化逻辑
        }

        // 点击二维码放大预览
        private void QrImage_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Image img) return;

            // 取出原图像源
            ImageSource src = img.Source;
            if (src == null) return;

            // 预览窗口
            Window preview = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                AllowsTransparency = true,
                ShowInTaskbar = false,
                Width = SystemParameters.PrimaryScreenWidth * 0.8,
                Height = SystemParameters.PrimaryScreenHeight * 0.8,
            };

            Grid root = new Grid();
            // 点击任意处关闭
            root.Background = Brushes.Transparent;
            root.MouseDown += (_, __) => preview.Close();

            Border card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.5
                },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Image big = new Image
            {
                Source = src,
                Stretch = Stretch.Uniform,
                Width = Math.Min(520, SystemParameters.PrimaryScreenWidth * 0.6),
                Height = Math.Min(520, SystemParameters.PrimaryScreenHeight * 0.6),
                Cursor = Cursors.Hand,
            };
            big.MouseLeftButtonUp += (_, __) => preview.Close();

            card.Child = big;
            root.Children.Add(card);

            preview.Content = root;
            preview.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    preview.Close();
                }
            };

            preview.ShowDialog();
        }
    }
}
