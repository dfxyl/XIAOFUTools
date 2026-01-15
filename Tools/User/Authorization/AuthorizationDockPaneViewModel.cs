using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace XIAOFUTools.Tools.Authorization
{
    /// <summary>
    /// 授权管理停靠窗格视图模型
    /// </summary>
    internal class AuthorizationDockPaneViewModel : INotifyPropertyChanged
    {
        private AuthorizationStatus _authStatus;
        private string _inputAuthCode = string.Empty;
        private bool _isProcessing = false;

        public AuthorizationDockPaneViewModel()
        {
            RefreshAuthorizationStatus();
        }

        #region 属性

        /// <summary>
        /// 授权状态
        /// </summary>
        public AuthorizationStatus AuthStatus
        {
            get => _authStatus;
            set
            {
                _authStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAuthorized));
                OnPropertyChanged(nameof(IsNotAuthorized));
            }
        }

        /// <summary>
        /// 输入的授权码
        /// </summary>
        public string InputAuthCode
        {
            get => _inputAuthCode;
            set
            {
                _inputAuthCode = value;
                OnPropertyChanged();
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                OnPropertyChanged();
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// 是否已授权
        /// </summary>
        public bool IsAuthorized => AuthStatus?.IsAuthorized ?? false;

        /// <summary>
        /// 是否未授权
        /// </summary>
        public bool IsNotAuthorized => !IsAuthorized;

        #endregion

        #region 命令

        /// <summary>
        /// 授权命令
        /// </summary>
        private ICommand _authorizeCommand;
        public ICommand AuthorizeCommand
        {
            get
            {
                return _authorizeCommand ?? (_authorizeCommand = new RelayCommand(ExecuteAuthorize, CanExecuteAuthorize));
            }
        }

        /// <summary>
        /// 取消授权命令
        /// </summary>
        private ICommand _revokeAuthorizationCommand;
        public ICommand RevokeAuthorizationCommand
        {
            get
            {
                return _revokeAuthorizationCommand ?? (_revokeAuthorizationCommand = new RelayCommand(ExecuteRevokeAuthorization, CanExecuteRevokeAuthorization));
            }
        }

        /// <summary>
        /// 刷新命令
        /// </summary>
        private ICommand _refreshCommand;
        public ICommand RefreshCommand
        {
            get
            {
                return _refreshCommand ?? (_refreshCommand = new RelayCommand(ExecuteRefresh, CanExecuteRefresh));
            }
        }

        /// <summary>
        /// 复制机器码命令
        /// </summary>
        private ICommand _copyMachineCodeCommand;
        public ICommand CopyMachineCodeCommand
        {
            get
            {
                return _copyMachineCodeCommand ?? (_copyMachineCodeCommand = new RelayCommand(ExecuteCopyMachineCode));
            }
        }



        /// <summary>
        /// 清空输入命令
        /// </summary>
        private ICommand _clearInputCommand;
        public ICommand ClearInputCommand
        {
            get
            {
                return _clearInputCommand ?? (_clearInputCommand = new RelayCommand(ExecuteClearInput));
            }
        }

        #endregion

        #region 命令实现

        private bool CanExecuteAuthorize()
        {
            return !IsProcessing && !string.IsNullOrWhiteSpace(InputAuthCode);
        }

        private void ExecuteAuthorize()
        {
            try
            {
                IsProcessing = true;

                // 验证授权码
                string machineCode = AuthorizationManager.GetMachineCode();
                var (isValid, expireTime, authType) = AuthorizationManager.ValidateAuthCode(InputAuthCode.Trim(), machineCode);

                if (!isValid)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("授权码无效，请检查输入的授权码是否正确。", "授权失败");
                    return;
                }

                if (DateTime.Now > expireTime)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"授权码已过期（过期时间：{expireTime:yyyy-MM-dd HH:mm:ss}）。", "授权失败");
                    return;
                }

                // 保存授权信息
                AuthorizationManager.SaveAuthInfo(InputAuthCode.Trim(), expireTime, machineCode, authType);

                // 刷新状态
                RefreshAuthorizationStatus();

                // 保留输入框内容，不清空
                // InputAuthCode = string.Empty; // 注释掉清空操作

                string typeDesc = authType == "通用版" ? "（通用版）" : "（个人版）";
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"授权成功{typeDesc}！\n授权类型：{authType}\n过期时间：{expireTime:yyyy-MM-dd HH:mm:ss}", "授权成功");
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"授权过程中出现错误：{ex.Message}", "授权失败");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private bool CanExecuteRevokeAuthorization()
        {
            return !IsProcessing && IsAuthorized;
        }

        private void ExecuteRevokeAuthorization()
        {
            try
            {
                var result = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    "确定要取消授权吗？取消后将无法使用相关功能。", 
                    "确认取消授权", 
                    System.Windows.MessageBoxButton.YesNo, 
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    IsProcessing = true;

                    // 清除授权信息
                    AuthorizationManager.ClearAuthInfo();

                    // 清空输入框
                    InputAuthCode = string.Empty;

                    // 刷新状态
                    RefreshAuthorizationStatus();

                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("授权已取消。", "取消授权");
                }
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"取消授权时出现错误：{ex.Message}", "错误");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private bool CanExecuteRefresh()
        {
            return !IsProcessing;
        }

        private void ExecuteRefresh()
        {
            RefreshAuthorizationStatus();
        }

        private void ExecuteCopyMachineCode()
        {
            try
            {
                if (!string.IsNullOrEmpty(AuthStatus?.MachineCode))
                {
                    System.Windows.Clipboard.SetText(AuthStatus.MachineCode);
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("机器码已复制到剪贴板。", "复制成功");
                }
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"复制机器码时出现错误：{ex.Message}", "复制失败");
            }
        }



        private void ExecuteClearInput()
        {
            InputAuthCode = string.Empty;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 刷新授权状态
        /// </summary>
        private void RefreshAuthorizationStatus()
        {
            try
            {
                AuthStatus = AuthorizationManager.GetAuthorizationStatus();

                // 始终显示内置的默认通用授权码
                InputAuthCode = "UNIVERSAL_RFFjSUdRTUhEQUlETVdaL2ZHcG9jM0ZzWmdZQ0FnUnZZM1Z6WlM0dGJBZ1dEbkluTXpFTkd5ZzRjR0U0TnlJeEhVTjdlbUpxQ2gwbUJqUVVIQThnTFRNRg==";
            }
            catch (Exception ex)
            {
                AuthStatus = new AuthorizationStatus
                {
                    IsAuthorized = false,
                    Message = $"获取授权状态失败：{ex.Message}",
                    ExpireTime = DateTime.MinValue,
                    RemainingDays = 0,
                    MachineCode = string.Empty
                };

                // 即使出现异常，也设置默认授权码
                InputAuthCode = "UNIVERSAL_RFFjSUdRTUhEQUlETVdaL2ZHcG9jM0ZzWmdZQ0FnUnZZM1Z6WlM0dGJBZ1dEbkluTXpFTkd5ZzRjR0U0TnlJeEhVTjdlbUpxQ2gwbUJqUVVIQThnTFRNRg==";
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion


    }

    /// <summary>
    /// RelayCommand实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { System.Windows.Input.CommandManager.RequerySuggested += value; }
            remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }
}
