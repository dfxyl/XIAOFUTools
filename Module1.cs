using System;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Core.Events;
using XIAOFUTools.Tools.PluginUpdate;
using XIAOFUTools.Tools.Settings;

namespace XIAOFUTools
{
    internal class Module1 : Module
    {
        private static Module1 _this = null;

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        public static Module1 Current => _this ??= (Module1)FrameworkApplication.FindModule("XIAOFUTools_Module");

        #region Overrides

        /// <summary>
        /// Called by Framework when ArcGIS Pro is initializing
        /// </summary>
        /// <returns>True if initialization is successful</returns>
        protected override bool Initialize()
        {
            // 订阅项目打开事件（参考原代码的实现方式）
            ProjectOpenedEvent.Subscribe(OnProjectOpened);
            
            // 在应用程序启动时检查更新
            System.Diagnostics.Debug.WriteLine("应用程序启动，开始检查更新...");
            _ = CheckForUpdatesAsync();
            
            return true;
        }

        /// <summary>
        /// 项目打开时触发的逻辑
        /// </summary>
        private async void OnProjectOpened(ProjectEventArgs args)
        {
            // 在项目打开时执行检查更新的逻辑
            await CheckForUpdatesAsync();
        }

        /// <summary>
        /// 检查更新（参考原代码的实现）
        /// </summary>
        public static async Task CheckForUpdatesAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"检查更新设置 - 启动时检查: {SettingsManager.Settings.PluginUpdate.CheckForUpdatesOnStartup}");
                
                // 检查用户设置是否启用启动时检查更新
                if (!SettingsManager.Settings.PluginUpdate.CheckForUpdatesOnStartup)
                {
                    System.Diagnostics.Debug.WriteLine("启动时检查更新已禁用");
                    return;
                }

                // 检测网络连接状态
                if (!IsNetworkAvailable())
                {
                    System.Diagnostics.Debug.WriteLine("网络不可用，跳过更新检查");
                    return; // 如果没有网络，直接跳过
                }

                // 检查是否需要检查（根据用户设置的间隔时间，0表示每次都检查）
                var settings = SettingsManager.Settings.PluginUpdate;
                System.Diagnostics.Debug.WriteLine($"上次检查时间: {settings.LastCheckTime?.ToString() ?? "从未检查"}");
                System.Diagnostics.Debug.WriteLine($"检查间隔: {settings.CheckInterval} 天");
                
                if (settings.LastCheckTime.HasValue && settings.CheckInterval > 0)
                {
                    var daysSinceLastCheck = (DateTime.Now - settings.LastCheckTime.Value).TotalDays;
                    System.Diagnostics.Debug.WriteLine($"距离上次检查: {daysSinceLastCheck:F1} 天，设置间隔: {settings.CheckInterval} 天");

                    if (daysSinceLastCheck < settings.CheckInterval)
                    {
                        System.Diagnostics.Debug.WriteLine("还未到检查时间，跳过检查");
                        return; // 还没到检查时间
                    }
                }
                else if (settings.CheckInterval == 0)
                {
                    System.Diagnostics.Debug.WriteLine("检查间隔设置为0，每次都检查更新");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("首次检查更新");
                }

                // 执行检查更新
                System.Diagnostics.Debug.WriteLine("开始执行更新检查");
                await UpdateChecker.CheckForUpdatesOnStartupAsync();
            }
            catch (Exception ex)
            {
                // 如果出现异常（例如网络问题），跳过不做任何操作
                System.Diagnostics.Debug.WriteLine($"检查更新时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 检测当前是否有网络连接
        /// </summary>
        /// <returns>如果网络可用，返回 true；否则返回 false</returns>
        private static bool IsNetworkAvailable()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                return false; // 如果发生异常，假设没有网络连接
            }
        }

        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            //TODO - add your business logic
            //return false to ~cancel~ Application close
            return true;
        }

        #endregion Overrides

    }
}
