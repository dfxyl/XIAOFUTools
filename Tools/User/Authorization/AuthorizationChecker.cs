using System;

namespace XIAOFUTools.Tools.Authorization
{
    /// <summary>
    /// 授权检测类 - 供其他工具简单调用
    /// </summary>
    public static class AuthorizationChecker
    {
        /// <summary>
        /// 检查是否已授权（其他工具只需调用这一个方法）
        /// </summary>
        /// <returns>是否已授权</returns>
        public static bool IsAuthorized()
        {
            try
            {
                var status = AuthorizationManager.GetAuthorizationStatus();
                return status.IsAuthorized;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查是否已授权并返回详细信息
        /// </summary>
        /// <returns>授权状态信息</returns>
        public static AuthorizationStatus GetAuthorizationStatus()
        {
            try
            {
                return AuthorizationManager.GetAuthorizationStatus();
            }
            catch (Exception ex)
            {
                return new AuthorizationStatus
                {
                    IsAuthorized = false,
                    Message = $"授权检查失败: {ex.Message}",
                    ExpireTime = DateTime.MinValue,
                    RemainingDays = 0,
                    MachineCode = string.Empty
                };
            }
        }

        /// <summary>
        /// 检查是否已授权，如果未授权则显示提示信息
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <returns>是否已授权</returns>
        public static bool CheckAuthorizationWithPrompt(string toolName = "此工具")
        {
            try
            {
                var status = GetAuthorizationStatus();

                if (!status.IsAuthorized)
                {
                    // 显示自定义授权提示对话框
                    var result = AuthorizationPromptDialog.ShowDialog(toolName, status);

                    // 如果用户选择打开授权界面
                    if (result == AuthorizationPromptResult.OpenAuthInterface)
                    {
                        OpenAuthorizationManager();
                    }

                    return false;
                }

                // 检查是否即将过期（7天内）
                if (status.RemainingDays <= 7 && status.RemainingDays > 0)
                {
                    // 显示自定义过期提醒对话框
                    var expireResult = AuthorizationExpiryDialog.ShowDialog(status.RemainingDays, status.ExpireTime);

                    if (expireResult == AuthorizationExpiryResult.OpenAuthInterface)
                    {
                        OpenAuthorizationManager();
                    }
                }

                return true;
            }
            catch
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"{toolName}授权检查失败，请联系技术支持。",
                    "授权检查错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);

                return false;
            }
        }

        /// <summary>
        /// 获取机器码（供用户获取机器码使用）
        /// </summary>
        /// <returns>机器码</returns>
        public static string GetMachineCode()
        {
            try
            {
                return AuthorizationManager.GetMachineCode();
            }
            catch
            {
                return "获取机器码失败";
            }
        }

        /// <summary>
        /// 获取剩余授权天数
        /// </summary>
        /// <returns>剩余天数，-1表示未授权或获取失败</returns>
        public static int GetRemainingDays()
        {
            try
            {
                var status = GetAuthorizationStatus();
                return status.IsAuthorized ? status.RemainingDays : -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 检查授权是否即将过期（7天内）
        /// </summary>
        /// <returns>是否即将过期</returns>
        public static bool IsAuthorizationExpiringSoon()
        {
            try
            {
                var status = GetAuthorizationStatus();
                return status.IsAuthorized && status.RemainingDays <= 7 && status.RemainingDays > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 打开授权管理界面
        /// </summary>
        public static void OpenAuthorizationManager()
        {
            try
            {
                AuthorizationDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"打开授权管理界面失败: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
