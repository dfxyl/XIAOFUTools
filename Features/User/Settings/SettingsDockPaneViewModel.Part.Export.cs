using System;
using System.ComponentModel;
using System.Windows.Input;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Features.User.PluginUpdate;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.User.Settings
{
    public partial class SettingsDockPaneViewModel
    {

        /// <summary>
        /// 应用设置
        /// </summary>
        private void ApplySettings()
        {
            SettingsManager.SaveSettings();

            PresentationServices.Dialogs.Show(
                "设置已保存。",
                "应用完成",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }
}
