using System;
using System.ComponentModel;
using System.Windows;

namespace XIAOFUTools.Tools.Authorization
{
    /// <summary>
    /// 授权过期提醒对话框
    /// </summary>
    public partial class AuthorizationExpiryDialog : Window, INotifyPropertyChanged
    {
        #region 属性

        private string _remainingTimeMessage;
        public string RemainingTimeMessage
        {
            get => _remainingTimeMessage;
            set
            {
                _remainingTimeMessage = value;
                OnPropertyChanged(nameof(RemainingTimeMessage));
            }
        }

        private string _expireTimeMessage;
        public string ExpireTimeMessage
        {
            get => _expireTimeMessage;
            set
            {
                _expireTimeMessage = value;
                OnPropertyChanged(nameof(ExpireTimeMessage));
            }
        }

        /// <summary>
        /// 对话框结果
        /// </summary>
        public AuthorizationExpiryResult Result { get; private set; } = AuthorizationExpiryResult.RemindLater;

        #endregion

        #region 构造函数

        public AuthorizationExpiryDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>
        /// 创建授权过期提醒对话框
        /// </summary>
        /// <param name="remainingDays">剩余天数</param>
        /// <param name="expireTime">过期时间</param>
        public AuthorizationExpiryDialog(int remainingDays, DateTime expireTime) : this()
        {
            if (remainingDays == 0)
            {
                RemainingTimeMessage = "授权今天过期！";
            }
            else if (remainingDays == 1)
            {
                RemainingTimeMessage = "授权明天过期！";
            }
            else
            {
                RemainingTimeMessage = $"剩余时间: {remainingDays} 天";
            }
            
            ExpireTimeMessage = $"过期时间: {expireTime:yyyy-MM-dd HH:mm:ss}";
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 打开授权界面按钮点击事件
        /// </summary>
        private void OpenAuthButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Result = AuthorizationExpiryResult.OpenAuthInterface;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开授权界面时出错: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 稍后提醒按钮点击事件
        /// </summary>
        private void RemindLaterButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AuthorizationExpiryResult.RemindLater;
            DialogResult = false;
            Close();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region 静态方法

        /// <summary>
        /// 显示授权过期提醒对话框
        /// </summary>
        /// <param name="remainingDays">剩余天数</param>
        /// <param name="expireTime">过期时间</param>
        /// <param name="owner">父窗口</param>
        /// <returns>对话框结果</returns>
        public static AuthorizationExpiryResult ShowDialog(int remainingDays, DateTime expireTime, Window owner = null)
        {
            try
            {
                var dialog = new AuthorizationExpiryDialog(remainingDays, expireTime);
                
                if (owner != null)
                {
                    dialog.Owner = owner;
                }
                else
                {
                    // 尝试获取ArcGIS Pro主窗口作为父窗口
                    try
                    {
                        var mainWindow = Application.Current?.MainWindow;
                        if (mainWindow != null)
                        {
                            dialog.Owner = mainWindow;
                        }
                    }
                    catch
                    {
                        // 忽略获取主窗口失败的错误
                    }
                }

                dialog.ShowDialog();
                return dialog.Result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"授权过期对话框失败: {ex.Message}");
                // 如果自定义对话框失败，回退到标准MessageBox
                string warningMessage = $"授权即将过期！\n\n";
                warningMessage += $"剩余时间: {remainingDays} 天\n";
                warningMessage += $"过期时间: {expireTime:yyyy-MM-dd HH:mm:ss}\n\n";
                warningMessage += "请及时联系作者续期授权。\n\n";
                warningMessage += "是否现在打开授权界面进行续期？";
                
                var result = MessageBox.Show(warningMessage, "授权即将过期", 
                    MessageBoxButton.YesNo, MessageBoxImage.Information);
                
                return result == MessageBoxResult.Yes ? 
                    AuthorizationExpiryResult.OpenAuthInterface : 
                    AuthorizationExpiryResult.RemindLater;
            }
        }

        #endregion
    }

    /// <summary>
    /// 授权过期提醒对话框结果
    /// </summary>
    public enum AuthorizationExpiryResult
    {
        /// <summary>
        /// 稍后提醒
        /// </summary>
        RemindLater,
        
        /// <summary>
        /// 打开授权界面
        /// </summary>
        OpenAuthInterface
    }
}
