using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.Authorization
{
    /// <summary>
    /// 授权检测使用示例 - 展示如何在其他工具中使用授权检测
    /// </summary>
    internal class ExampleToolButton : Button
    {
        protected override void OnClick()
        {
            // 方法1：简单检查（推荐）
            if (!AuthorizationChecker.CheckAuthorizationWithPrompt("示例工具"))
            {
                return; // 未授权，已显示提示信息，直接返回
            }

            // 授权通过，执行工具功能
            ExecuteToolFunction();
        }

        private void ExecuteToolFunction()
        {
            try
            {
                // 工具的实际功能代码
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("工具执行成功！", "示例工具");
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"工具执行失败: {ex.Message}", "错误");
            }
        }
    }

    /// <summary>
    /// 另一种使用方式的示例
    /// </summary>
    internal class AnotherExampleButton : Button
    {
        protected override void OnClick()
        {
            // 方法2：只检查授权状态，不显示提示
            if (!AuthorizationChecker.IsAuthorized())
            {
                // 自定义未授权处理
                var status = AuthorizationChecker.GetAuthorizationStatus();
                string message = $"此工具需要授权才能使用。\n\n状态: {status.Message}\n机器码: {status.MachineCode}";
                
                var result = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    message + "\n\n是否打开授权管理界面？", 
                    "需要授权", 
                    System.Windows.MessageBoxButton.YesNo, 
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    AuthorizationChecker.OpenAuthorizationManager();
                }
                return;
            }

            // 检查是否即将过期
            if (AuthorizationChecker.IsAuthorizationExpiringSoon())
            {
                int remainingDays = AuthorizationChecker.GetRemainingDays();
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"授权即将在 {remainingDays} 天后过期，请及时续期。", 
                    "授权提醒", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Information);
            }

            // 执行工具功能
            ExecuteAnotherToolFunction();
        }

        private void ExecuteAnotherToolFunction()
        {
            // 工具功能代码
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("另一个工具执行成功！", "另一个示例工具");
        }
    }

    /// <summary>
    /// DockPane中的使用示例
    /// </summary>
    internal class ExampleDockPaneViewModel
    {
        public ExampleDockPaneViewModel()
        {
            // 在ViewModel构造函数中检查授权
            CheckAuthorization();
        }

        private void CheckAuthorization()
        {
            if (!AuthorizationChecker.IsAuthorized())
            {
                // 可以设置界面状态，禁用功能等
                IsToolEnabled = false;
                StatusMessage = "需要授权才能使用此功能";
            }
            else
            {
                IsToolEnabled = true;
                var status = AuthorizationChecker.GetAuthorizationStatus();
                StatusMessage = $"授权有效，{status.RemainingTimeDescription}";
            }
        }

        public bool IsToolEnabled { get; set; }
        public string StatusMessage { get; set; }

        public void ExecuteCommand()
        {
            // 在命令执行时再次检查授权
            if (!AuthorizationChecker.CheckAuthorizationWithPrompt("DockPane工具"))
            {
                return;
            }

            // 执行命令
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("DockPane命令执行成功！", "示例DockPane");
        }
    }
}

/*
使用说明：

1. 最简单的使用方式（推荐）：
   if (!AuthorizationChecker.CheckAuthorizationWithPrompt("工具名称"))
       return;

2. 只检查授权状态：
   if (!AuthorizationChecker.IsAuthorized())
       // 处理未授权情况

3. 获取详细授权信息：
   var status = AuthorizationChecker.GetAuthorizationStatus();

4. 检查是否即将过期：
   if (AuthorizationChecker.IsAuthorizationExpiringSoon())
       // 提醒用户续期

5. 获取机器码：
   string machineCode = AuthorizationChecker.GetMachineCode();

6. 打开授权管理界面：
   AuthorizationChecker.OpenAuthorizationManager();

注意事项：
- 在工具的OnClick方法开始处调用授权检查
- 在DockPane的ViewModel构造函数中检查授权状态
- 对于长时间运行的工具，可以在关键操作前再次检查授权
- 授权检查失败时，应该停止工具执行并给用户适当提示
*/
