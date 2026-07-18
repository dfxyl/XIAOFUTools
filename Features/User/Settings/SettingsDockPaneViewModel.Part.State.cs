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
        /// 只有选择要素时才能打开窗口
        /// </summary>
        public bool RequireSelectionToOpen
        {
            get => SettingsManager.Settings.ViewArea.RequireSelectionToOpen;
            set
            {
                if (SettingsManager.Settings.ViewArea.RequireSelectionToOpen != value)
                {
                    SettingsManager.Settings.ViewArea.RequireSelectionToOpen = value;
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 取消选择后自动关闭窗口
        /// </summary>
        public bool AutoCloseOnClearSelection
        {
            get => SettingsManager.Settings.ViewArea.AutoCloseOnClearSelection;
            set
            {
                if (SettingsManager.Settings.ViewArea.AutoCloseOnClearSelection != value)
                {
                    SettingsManager.Settings.ViewArea.AutoCloseOnClearSelection = value;
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 默认小数位数
        /// </summary>
        public int DefaultDecimalPlaces
        {
            get => SettingsManager.Settings.ViewArea.DefaultDecimalPlaces;
            set
            {
                if (SettingsManager.Settings.ViewArea.DefaultDecimalPlaces != value && value >= 0 && value <= 10)
                {
                    SettingsManager.Settings.ViewArea.DefaultDecimalPlaces = value;
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 自动保存用户偏好
        /// </summary>
        public bool AutoSavePreferences
        {
            get => SettingsManager.Settings.General.AutoSavePreferences;
            set
            {
                if (SettingsManager.Settings.General.AutoSavePreferences != value)
                {
                    SettingsManager.Settings.General.AutoSavePreferences = value;
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 显示工具提示
        /// </summary>
        public bool ShowTooltips
        {
            get => SettingsManager.Settings.General.ShowTooltips;
            set
            {
                if (SettingsManager.Settings.General.ShowTooltips != value)
                {
                    SettingsManager.Settings.General.ShowTooltips = value;
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 显示 GIS 工具口袋功能区标签页
        /// </summary>
        public bool ShowGisToolPocketRibbonTab
        {
            get => SettingsManager.Settings.GisToolPocket.ShowRibbonTab;
            set
            {
                if (SettingsManager.Settings.GisToolPocket.ShowRibbonTab != value)
                {
                    SettingsManager.Settings.GisToolPocket.ShowRibbonTab = value;
                    RibbonStateService.SetState(RibbonStateService.GisToolPocketTabVisibleStateId, value);
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 自动刷新图层列表
        /// </summary>
        public bool AutoRefreshLayers
        {
            get => SettingsManager.Settings.PresetLayers.AutoRefreshLayers;
            set
            {
                if (SettingsManager.Settings.PresetLayers.AutoRefreshLayers != value)
                {
                    SettingsManager.Settings.PresetLayers.AutoRefreshLayers = value;
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 添加图层后自动移动到底部
        /// </summary>
        public bool MoveLayersToBottom
        {
            get => SettingsManager.Settings.PresetLayers.MoveLayersToBottom;
            set
            {
                if (SettingsManager.Settings.PresetLayers.MoveLayersToBottom != value)
                {
                    SettingsManager.Settings.PresetLayers.MoveLayersToBottom = value;
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 预设图层文件夹路径
        /// </summary>
        public string LayersPath
        {
            get
            {
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string installPath = Path.GetDirectoryName(assemblyLocation);
                return Path.GetFullPath(Path.Combine(installPath, SettingsManager.Settings.PresetLayers.LayersPath));
            }
        }

        /// <summary>
        /// 自动检测更新
        /// </summary>
        public bool AutoCheckForUpdates
        {
            get => SettingsManager.Settings.PluginUpdate.AutoCheckForUpdates;
            set
            {
                if (SettingsManager.Settings.PluginUpdate.AutoCheckForUpdates != value)
                {
                    SettingsManager.Settings.PluginUpdate.AutoCheckForUpdates = value;
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 检查间隔（天数）0=每次启动都检查，1=每天，7=每周，30=每月
        /// </summary>
        public int CheckInterval
        {
            get => SettingsManager.Settings.PluginUpdate.CheckInterval;
            set
            {
                if (SettingsManager.Settings.PluginUpdate.CheckInterval != value && value >= 0 && value <= 30)
                {
                    SettingsManager.Settings.PluginUpdate.CheckInterval = value;
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 发现更新时通知用户
        /// </summary>
        public bool NotifyOnUpdate
        {
            get => SettingsManager.Settings.PluginUpdate.NotifyOnUpdate;
            set
            {
                if (SettingsManager.Settings.PluginUpdate.NotifyOnUpdate != value)
                {
                    SettingsManager.Settings.PluginUpdate.NotifyOnUpdate = value;
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 启动时检查更新
        /// </summary>
        public bool CheckForUpdatesOnStartup
        {
            get => SettingsManager.Settings.PluginUpdate.CheckForUpdatesOnStartup;
            set
            {
                if (SettingsManager.Settings.PluginUpdate.CheckForUpdatesOnStartup != value)
                {
                    SettingsManager.Settings.PluginUpdate.CheckForUpdatesOnStartup = value;
                    NotifyPropertyChanged();
                    SettingsManager.SaveSettings();
                }
            }
        }

        /// <summary>
        /// 上次检查时间显示文本
        /// </summary>
        public string LastCheckTimeText
        {
            get
            {
                var lastCheck = SettingsManager.Settings.PluginUpdate.LastCheckTime;
                if (lastCheck.HasValue)
                {
                    return $"上次检查: {lastCheck.Value:yyyy-MM-dd HH:mm}";
                }
                return "从未检查";
            }
        }

        public string CurrentArcGISProVersionText
        {
            get
            {
                var v = XIAOFUTools.Features.User.PluginUpdate.UpdateChecker.GetCurrentDesktopVersion();
                return string.IsNullOrWhiteSpace(v) ? "ArcGIS Pro版本: 未知" : $"ArcGIS Pro版本: {v}";
            }
        }

        public ICommand ResetCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand ImportLayersCommand { get; }
        public ICommand OpenLayersFolderCommand { get; }
        public ICommand CheckUpdateCommand { get; }
    }
}
