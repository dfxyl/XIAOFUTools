using System;
using System.IO;
using System.Text.Json;
using ArcGIS.Desktop.Framework;

namespace XIAOFUTools.Tools.Settings
{
    /// <summary>
    /// 设置管理器
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIAOFUTools",
            "settings.json"
        );

        private static UserSettings _settings;

        /// <summary>
        /// 获取当前设置
        /// </summary>
        public static UserSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    LoadSettings();
                }
                return _settings;
            }
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        private static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    _settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                }
                else
                {
                    _settings = new UserSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
                _settings = new UserSettings();
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        public static void SaveSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置设置为默认值
        /// </summary>
        public static void ResetSettings()
        {
            _settings = new UserSettings();
            SaveSettings();
        }
    }

    /// <summary>
    /// 用户设置类
    /// </summary>
    public class UserSettings
    {
        /// <summary>
        /// 查看面积工具设置
        /// </summary>
        public ViewAreaSettings ViewArea { get; set; } = new ViewAreaSettings();

        /// <summary>
        /// 通用设置
        /// </summary>
        public GeneralSettings General { get; set; } = new GeneralSettings();

        /// <summary>
        /// 预设图层设置
        /// </summary>
        public PresetLayersSettings PresetLayers { get; set; } = new PresetLayersSettings();

        /// <summary>
        /// 插件更新设置
        /// </summary>
        public PluginUpdateSettings PluginUpdate { get; set; } = new PluginUpdateSettings();

        /// <summary>
        /// 浏览要素工具设置
        /// </summary>
        public BrowseFeaturesSettings BrowseFeatures { get; set; } = new BrowseFeaturesSettings();
    }

    /// <summary>
    /// 查看面积工具设置
    /// </summary>
    public class ViewAreaSettings
    {
        /// <summary>
        /// 只有选择要素时才能打开窗口
        /// </summary>
        public bool RequireSelectionToOpen { get; set; } = true;

        /// <summary>
        /// 取消选择后自动关闭窗口
        /// </summary>
        public bool AutoCloseOnClearSelection { get; set; } = true;

        /// <summary>
        /// 默认小数位数
        /// </summary>
        public int DefaultDecimalPlaces { get; set; } = 2;
    }

    /// <summary>
    /// 通用设置
    /// </summary>
    public class GeneralSettings
    {
        /// <summary>
        /// 自动保存用户偏好
        /// </summary>
        public bool AutoSavePreferences { get; set; } = true;

        /// <summary>
        /// 显示工具提示
        /// </summary>
        public bool ShowTooltips { get; set; } = true;
    }

    /// <summary>
    /// 预设图层设置
    /// </summary>
    public class PresetLayersSettings
    {
        /// <summary>
        /// 预设图层文件夹路径（相对于程序集位置）
        /// </summary>
        public string LayersPath { get; set; } = @"Data\影像图层";

        /// <summary>
        /// 自动刷新图层列表
        /// </summary>
        public bool AutoRefreshLayers { get; set; } = true;

        /// <summary>
        /// 添加图层后自动移动到底部
        /// </summary>
        public bool MoveLayersToBottom { get; set; } = true;
    }

    /// <summary>
    /// 插件更新设置
    /// </summary>
    public class PluginUpdateSettings
    {
        /// <summary>
        /// 自动检测更新
        /// </summary>
        public bool AutoCheckForUpdates { get; set; } = true;

        /// <summary>
        /// 检查间隔（天数）1=每天，7=每周，30=每月
        /// </summary>
        public int CheckInterval { get; set; } = 1;

        /// <summary>
        /// 上次检查时间
        /// </summary>
        public DateTime? LastCheckTime { get; set; } = null;

        /// <summary>
        /// 发现更新时通知用户
        /// </summary>
        public bool NotifyOnUpdate { get; set; } = true;

        /// <summary>
        /// 启动时检查更新
        /// </summary>
        public bool CheckForUpdatesOnStartup { get; set; } = true;
    }

    /// <summary>
    /// 浏览要素工具设置
    /// </summary>
    public class BrowseFeaturesSettings
    {
        /// <summary>
        /// 默认审阅人
        /// </summary>
        public string ReviewerName { get; set; } = string.Empty;

        /// <summary>
        /// 输出GDB路径
        /// </summary>
        public string OutputGdbPath { get; set; } = string.Empty;

        /// <summary>
        /// 输出表名
        /// </summary>
        public string OutputTableName { get; set; } = string.Empty;

        /// <summary>
        /// 默认批次ID
        /// </summary>
        public string BatchId { get; set; } = string.Empty;

        /// <summary>
        /// 是否将备注写回要素字段
        /// </summary>
        public bool WriteNotesToFeatureField { get; set; } = false;

        /// <summary>
        /// 备注回写字段名
        /// </summary>
        public string NotesFieldName { get; set; } = string.Empty;
    }
}
