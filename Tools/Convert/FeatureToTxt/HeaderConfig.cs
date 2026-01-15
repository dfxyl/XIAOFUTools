using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.FeatureToTxt
{
    /// <summary>
    /// 头部信息配置类
    /// </summary>
    public class HeaderConfig : PropertyChangedBase
    {
        private string _name = "默认配置";
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _headerText = @"[属性描述]
坐标系=2000国家大地坐标系
几度分带=3
投影类型=高斯克吕格
计量单位=米
带号=37
精度=0.001
转换参数=,,,,,,
[地块坐标]";
        
        public string HeaderText
        {
            get => _headerText;
            set => SetProperty(ref _headerText, value);
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public HeaderConfig Clone()
        {
            return new HeaderConfig
            {
                Name = this.Name,
                HeaderText = this.HeaderText
            };
        }
    }

    /// <summary>
    /// 头部信息配置管理器
    /// </summary>
    public static class HeaderConfigManager
    {
        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIAOFUTools",
            "FeatureToTxt_HeaderConfigs.json"
        );

        /// <summary>
        /// 获取默认配置
        /// </summary>
        public static HeaderConfig GetDefaultConfig()
        {
            return new HeaderConfig
            {
                Name = "默认配置",
                HeaderText = @"[属性描述]
坐标系=2000国家大地坐标系
几度分带=3
投影类型=高斯克吕格
计量单位=米
带号=37
精度=0.001
转换参数=,,,,,,
[地块坐标]"
            };
        }

        /// <summary>
        /// 加载所有配置
        /// </summary>
        public static List<HeaderConfig> LoadConfigs()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var configs = JsonSerializer.Deserialize<List<HeaderConfig>>(json);
                    if (configs != null && configs.Count > 0)
                    {
                        return configs;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载头部配置失败: {ex.Message}");
            }

            // 返回默认配置
            return new List<HeaderConfig> { GetDefaultConfig() };
        }

        /// <summary>
        /// 保存所有配置
        /// </summary>
        public static void SaveConfigs(List<HeaderConfig> configs)
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(configs, options);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存头部配置失败: {ex.Message}");
                throw;
            }
        }
    }
}
