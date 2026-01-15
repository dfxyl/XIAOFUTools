using System;
using System.ComponentModel;
using System.Windows;

namespace XIAOFUTools.Tools.Authorization
{
    /// <summary>
    /// 授权提示对话框
    /// </summary>
    public partial class AuthorizationPromptDialog : Window, INotifyPropertyChanged
    {
        #region 属性

        private string _title = "需要授权";
        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
            }
        }

        private string _toolMessage;
        public string ToolMessage
        {
            get => _toolMessage;
            set
            {
                _toolMessage = value;
                OnPropertyChanged(nameof(ToolMessage));
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        private string _machineCodeMessage;
        public string MachineCodeMessage
        {
            get => _machineCodeMessage;
            set
            {
                _machineCodeMessage = value;
                OnPropertyChanged(nameof(MachineCodeMessage));
            }
        }

        /// <summary>
        /// 对话框结果
        /// </summary>
        public AuthorizationPromptResult Result { get; private set; } = AuthorizationPromptResult.Cancel;

        #endregion

        #region 构造函数

        public AuthorizationPromptDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>
        /// 创建授权提示对话框
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="status">授权状态</param>
        public AuthorizationPromptDialog(string toolName, AuthorizationStatus status) : this()
        {
            ToolMessage = $"{toolName}需要授权才能使用。";
            StatusMessage = $"授权状态: {status.Message}";
            MachineCodeMessage = $"机器码: {status.MachineCode}";
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
                Result = AuthorizationPromptResult.OpenAuthInterface;
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
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = AuthorizationPromptResult.Cancel;
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
        /// 显示授权提示对话框
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="status">授权状态</param>
        /// <param name="owner">父窗口</param>
        /// <returns>对话框结果</returns>
        public static AuthorizationPromptResult ShowDialog(string toolName, AuthorizationStatus status, Window owner = null)
        {
            try
            {
                var dialog = new AuthorizationPromptDialog(toolName, status);
                
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
                // 如果自定义对话框失败，回退到标准MessageBox
                string message = $"{toolName}需要授权才能使用。\n\n";
                message += $"授权状态: {status.Message}\n";
                message += $"机器码: {status.MachineCode}\n\n";
                message += "请联系作者获取授权码，或在'用户'->'配置'->'授权'中进行授权。";
                
                MessageBox.Show(message, "需要授权", MessageBoxButton.OK, MessageBoxImage.Warning);
                return AuthorizationPromptResult.Cancel;
            }
        }

        #endregion
    }

    /// <summary>
    /// 授权提示对话框结果
    /// </summary>
    public enum AuthorizationPromptResult
    {
        /// <summary>
        /// 取消
        /// </summary>
        Cancel,
        
        /// <summary>
        /// 打开授权界面
        /// </summary>
        OpenAuthInterface
    }
}
