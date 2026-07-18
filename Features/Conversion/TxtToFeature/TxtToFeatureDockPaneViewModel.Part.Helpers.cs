using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.Conversion.TxtToFeature.Core;

namespace XIAOFUTools.Features.Conversion.TxtToFeature
{
    public partial class TxtToFeatureDockPaneViewModel
    {

        /// <summary>
        /// 清理文件名，移除ArcGIS不支持的字符
        /// </summary>
        private string CleanFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "output";

            // ArcGIS Shapefile文件名不支持的字符
            var invalidChars = new char[] { '-', ' ', '.', '(', ')', '[', ']', '{', '}', '!', '@', '#', '$', '%', '^', '&', '*', '+', '=', '|', '\\', '/', ':', ';', '"', '\'', '<', '>', '?', ',' };

            var cleanName = fileName;

            // 替换无效字符为下划线
            foreach (var invalidChar in invalidChars)
            {
                cleanName = cleanName.Replace(invalidChar, '_');
            }

            // 移除连续的下划线
            while (cleanName.Contains("__"))
            {
                cleanName = cleanName.Replace("__", "_");
            }

            // 移除开头和结尾的下划线
            cleanName = cleanName.Trim('_');

            // 确保不以数字开头
            if (cleanName.Length > 0 && char.IsDigit(cleanName[0]))
            {
                cleanName = "F_" + cleanName;
            }

            // 如果清理后为空，使用默认名称
            if (string.IsNullOrEmpty(cleanName))
            {
                cleanName = "output";
            }

            // 限制长度（Shapefile文件名建议不超过10个字符，但现代系统支持更长）
            if (cleanName.Length > 50)
            {
                cleanName = cleanName.Substring(0, 50).TrimEnd('_');
            }

            LogMessage($"文件名清理: '{fileName}' -> '{cleanName}'");
            return cleanName;
        }

        /// <summary>
        /// 清理地图中的临时图层
        /// </summary>
        private void CleanupTemporaryLayers()
        {
            try
            {
                var map = MapView.Active?.Map;
                if (map == null)
                {
                    LogMessage("当前没有活动地图，无需清理临时图层");
                    return;
                }

                var layersToRemove = new List<Layer>();

                // 查找包含"temp_"的图层
                foreach (var layer in map.GetLayersAsFlattenedList())
                {
                    if (layer.Name.Contains("temp_") ||
                        layer.Name.Contains("临时") ||
                        (layer is FeatureLayer featureLayer &&
                         featureLayer.GetFeatureClass()?.GetDatastore()?.GetPath()?.LocalPath?.Contains("temp_") == true))
                    {
                        layersToRemove.Add(layer);
                        LogMessage($"发现临时图层: {layer.Name}");
                    }
                }

                // 移除临时图层
                if (layersToRemove.Any())
                {
                    map.RemoveLayers(layersToRemove);
                    LogMessage($"已移除 {layersToRemove.Count} 个临时图层");
                }
                else
                {
                    LogMessage("未发现需要清理的临时图层");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"清理临时图层时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 验证输入参数
        /// </summary>
        private async Task<bool> ValidateInputsAsync()
        {
            if (string.IsNullOrEmpty(InputFolder) ||
                !await _fileStore.DirectoryExistsAsync(InputFolder, CancellationToken.None))
            {
                LogError("请选择有效的输入文件夹");
                return false;
            }

            if (!SaveToSourcePath && string.IsNullOrEmpty(OutputFolder))
            {
                LogError("请选择输出文件夹");
                return false;
            }

            if (SelectedSpatialReference == null)
            {
                LogError("请选择坐标系");
                return false;
            }

            var txtFiles = await _fileStore.FindTextFilesAsync(
                InputFolder,
                IncludeSubfolders,
                CancellationToken.None);
            if (txtFiles.Count == 0)
            {
                LogError("输入文件夹中没有找到TXT文件");
                return false;
            }

            return true;
        }

    }
}
