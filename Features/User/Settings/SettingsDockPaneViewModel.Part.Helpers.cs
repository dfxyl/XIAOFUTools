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
        /// 重置设置
        /// </summary>
        private void ResetSettings()
        {
            var result = PresentationServices.Dialogs.Show(
                "确定要重置所有设置为默认值吗？此操作无法撤销。",
                "确认重置",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                SettingsManager.ResetSettings();
                
                // 刷新所有属性
                NotifyPropertyChanged(nameof(RequireSelectionToOpen));
                NotifyPropertyChanged(nameof(AutoCloseOnClearSelection));
                NotifyPropertyChanged(nameof(DefaultDecimalPlaces));
                NotifyPropertyChanged(nameof(AutoSavePreferences));
                NotifyPropertyChanged(nameof(ShowTooltips));
                NotifyPropertyChanged(nameof(AutoRefreshLayers));
                NotifyPropertyChanged(nameof(MoveLayersToBottom));
                NotifyPropertyChanged(nameof(AutoCheckForUpdates));
                NotifyPropertyChanged(nameof(CheckInterval));
                NotifyPropertyChanged(nameof(NotifyOnUpdate));
                NotifyPropertyChanged(nameof(LastCheckTimeText));

                PresentationServices.Dialogs.Show(
                    "设置已重置为默认值。",
                    "重置完成",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
    }
}
