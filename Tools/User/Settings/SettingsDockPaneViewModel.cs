using System;
using System.ComponentModel;
using System.Windows.Input;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Tools.PluginUpdate;
using System.Threading.Tasks;

namespace XIAOFUTools.Tools.Settings
{
    /// <summary>
    /// 设置停靠窗格视图模型
    /// </summary>
    public class SettingsDockPaneViewModel : PropertyChangedBase
    {
        #region 查看面积工具设置

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

        #endregion

        #region 通用设置

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

        #endregion

        #region 预设图层设置

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

        #endregion

        #region 插件更新设置

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
                var v = XIAOFUTools.Tools.PluginUpdate.UpdateChecker.GetCurrentDesktopVersion();
                return string.IsNullOrWhiteSpace(v) ? "ArcGIS Pro版本: 未知" : $"ArcGIS Pro版本: {v}";
            }
        }

        #endregion

        #region 命令

        public ICommand ResetCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand ImportLayersCommand { get; }
        public ICommand OpenLayersFolderCommand { get; }
        public ICommand CheckUpdateCommand { get; }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public SettingsDockPaneViewModel()
        {
            // 初始化命令
            ResetCommand = new SettingsRelayCommand(ResetSettings);
            ApplyCommand = new SettingsRelayCommand(ApplySettings);
            ImportLayersCommand = new SettingsRelayCommand(ImportLayers);
            OpenLayersFolderCommand = new SettingsRelayCommand(OpenLayersFolder);
            CheckUpdateCommand = new SettingsRelayCommand(CheckForUpdates);
        }

        /// <summary>
        /// 重置设置
        /// </summary>
        private void ResetSettings()
        {
            var result = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
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

                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    "设置已重置为默认值。",
                    "重置完成",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 应用设置
        /// </summary>
        private void ApplySettings()
        {
            SettingsManager.SaveSettings();

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                "设置已保存。",
                "应用完成",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        /// <summary>
        /// 导入预设图层
        /// </summary>
        private void ImportLayers()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "选择预设图层文件",
                    Filter = "图层文件 (*.lyr;*.lyrx)|*.lyr;*.lyrx|ArcMap图层文件 (*.lyr)|*.lyr|ArcGIS Pro图层文件 (*.lyrx)|*.lyrx",
                    Multiselect = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string targetPath = LayersPath;

                    // 确保目标文件夹存在
                    if (!Directory.Exists(targetPath))
                    {
                        Directory.CreateDirectory(targetPath);
                    }

                    int successCount = 0;
                    int failCount = 0;

                    foreach (string sourceFile in openFileDialog.FileNames)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(sourceFile);
                            string destFile = Path.Combine(targetPath, fileName);

                            // 如果文件已存在，询问是否覆盖
                            if (File.Exists(destFile))
                            {
                                var result = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                                    $"文件 '{fileName}' 已存在，是否覆盖？",
                                    "文件已存在",
                                    System.Windows.MessageBoxButton.YesNo,
                                    System.Windows.MessageBoxImage.Question);

                                if (result != System.Windows.MessageBoxResult.Yes)
                                {
                                    continue;
                                }
                            }

                            File.Copy(sourceFile, destFile, true);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"复制文件失败: {ex.Message}");
                            failCount++;
                        }
                    }

                    string message = $"导入完成！\n成功: {successCount} 个文件";
                    if (failCount > 0)
                    {
                        message += $"\n失败: {failCount} 个文件";
                    }

                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        message,
                        "导入结果",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"导入预设图层时出错: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开预设图层文件夹
        /// </summary>
        private void OpenLayersFolder()
        {
            try
            {
                string folderPath = LayersPath;

                // 确保文件夹存在
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // 打开文件夹
                Process.Start("explorer.exe", folderPath);
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"打开文件夹时出错: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        private async void CheckForUpdates()
        {
            try
            {
                var pd = new ProgressDialog("正在检查更新", "取消", 100, true);
                var cps = new CancelableProgressorSource(pd);

                var updateInfo = await QueuedTask.Run(async () =>
                {
                    return await UpdateChecker.CheckForUpdatesWithProgressAsync(cps);
                }, cps.Progressor);

                // 更新最后检查时间
                SettingsManager.Settings.PluginUpdate.LastCheckTime = DateTime.Now;
                SettingsManager.SaveSettings();
                NotifyPropertyChanged(nameof(LastCheckTimeText));

                if (!string.IsNullOrEmpty(updateInfo.ErrorMessage))
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        updateInfo.ErrorMessage,
                        "检查更新失败",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                if (updateInfo.HasUpdate)
                {
                    var message = $"发现新版本！\n\n当前版本: {updateInfo.CurrentVersion}\n最新版本: {updateInfo.LatestVersion}";

                    if (!string.IsNullOrEmpty(updateInfo.ReleaseDate))
                    {
                        message += $"\n发布日期: {updateInfo.ReleaseDate}";
                    }

                    message += $"\n\n更新内容:\n{updateInfo.UpdateNotes}";

                    if (!string.IsNullOrEmpty(updateInfo.Notice))
                    {
                        message += $"\n\n{updateInfo.Notice}";
                    }

                    if (!updateInfo.IsDesktopVersionCompatible)
                    {
                        var currentDv = string.IsNullOrEmpty(updateInfo.CurrentDesktopVersion) ? "未知" : updateInfo.CurrentDesktopVersion;
                        var minDv = string.IsNullOrEmpty(updateInfo.MinDesktopVersion) ? "未知" : updateInfo.MinDesktopVersion;

                        message += $"\n\n当前 ArcGIS Pro: {currentDv}\n最低支持版本: {minDv}\n版本不匹配，仅显示更新信息（不提供下载）。";

                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                            message,
                            "发现更新",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                        return;
                    }

                    message += "\n\n是否立即下载并安装？";

                    var result = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        message,
                        "发现更新",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        // 直接下载并安装，不再弹窗
                        await UpdateChecker.DownloadAndInstallUpdateAsync(updateInfo);
                    }
                }
                else
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        $"当前已是最新版本 ({updateInfo.CurrentVersion})",
                        "检查更新",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"检查更新时出错: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// SettingsRelayCommand实现
    /// </summary>
    public class SettingsRelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public SettingsRelayCommand(Action execute, Func<bool> canExecute = null)
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
