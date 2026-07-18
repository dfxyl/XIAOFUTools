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
    /// <summary>
    /// 设置停靠窗格视图模型
    /// </summary>
    public partial class SettingsDockPaneViewModel : PropertyChangedBase
    {
        private readonly Infrastructure.PresetLayerFileStore _presetLayerFileStore = new Infrastructure.PresetLayerFileStore();

        /// <summary>
        /// 构造函数
        /// </summary>
        public SettingsDockPaneViewModel()
        {
            // 初始化命令
            ResetCommand = new RelayCommand(ResetSettings);
            ApplyCommand = new RelayCommand(ApplySettings);
            ImportLayersCommand = new RelayCommand(ImportLayers);
            OpenLayersFolderCommand = new RelayCommand(OpenLayersFolder);
            CheckUpdateCommand = new RelayCommand(CheckForUpdates);
        }
    }

    /// <summary>
    /// RelayCommand实现
    /// </summary>

}
