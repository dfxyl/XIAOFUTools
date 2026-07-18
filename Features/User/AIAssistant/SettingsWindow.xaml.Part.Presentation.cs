using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework.Controls;
using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Services;

namespace XIAOFUTools.Features.User.AIAssistant
{
    public partial class SettingsWindow
    {
        
        /// <summary>
        /// 显示操作成功提示（自动消失）
        /// </summary>
        private void ShowToast(string message, bool isSuccess = true)
        {
            try
            {
                toastBorder.Background = isSuccess 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dcfce7"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fee2e2"));
                toastText.Foreground = isSuccess
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16a34a"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dc2626"));
                toastText.Text = message;
                toastBorder.Visibility = Visibility.Visible;
                
                // 3秒后自动隐藏
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                timer.Tick += (s, e) =>
                {
                    toastBorder.Visibility = Visibility.Collapsed;
                    timer.Stop();
                };
                timer.Start();
            }
            catch { }
        }
        
        /// <summary>
        /// 取消
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
