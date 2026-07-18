using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using System.Globalization;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.BatchLayerClip
{
    internal partial class BatchLayerClipViewModel
    {
        
        /// <summary>
        /// 刷新图层列表（公共方法）
        /// </summary>
        public void RefreshLayers()
        {
            LoadFeatureLayers();
        }

        /// <summary>
        /// 更新字段名称列表
        /// </summary>
        private void UpdateFieldNames()
        {
            // 在UI线程清空字段列表
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                FieldNames.Clear();
            });
            
            // 如果没有选择图层，直接返回
            if (SelectedFeatureLayer == null)
                return;
                
            // 使用QueuedTask在后台线程执行
            QueuedTask.Run(() =>
            {
                try
                {
                    // 创建临时列表存储字段
                    var tempFields = new List<string>();
                    
                    using (var table = SelectedFeatureLayer.GetTable())
                    {
                        var definition = table.GetDefinition();
                        var fields = definition.GetFields();
                        
                        foreach (var field in fields)
                        {
                            // 只添加文本、整数和双精度字段作为分组字段
                            if (field.FieldType == FieldType.String || 
                                field.FieldType == FieldType.Integer ||
                                field.FieldType == FieldType.SmallInteger ||
                                field.FieldType == FieldType.Double ||
                                field.FieldType == FieldType.Single)
                            {
                                // 格式化字段显示名称为"字段名称(别名)"
                                string displayName;
                                if (string.IsNullOrEmpty(field.AliasName) || field.Name.Equals(field.AliasName, StringComparison.OrdinalIgnoreCase))
                                {
                                    displayName = field.Name;
                                }
                                else
                                {
                                    displayName = $"{field.Name}({field.AliasName})";
                                }
                                tempFields.Add(displayName);
                            }
                        }
                    }
                    
                    // 在UI线程更新字段列表
                    PresentationServices.UiThread.InvokeOrRun(() => 
                    {
                        // 将临时列表中的字段添加到字段名称列表
                        foreach (var field in tempFields)
                        {
                            FieldNames.Add(field);
                        }
                        
                        // 如果有字段，默认选择第一个
                        if (FieldNames.Count > 0)
                        {
                            SelectedField = FieldNames[0];
                        }
                    });
                }
                catch (Exception ex)
                {
                    // 确保在UI线程显示错误信息
                    PresentationServices.UiThread.InvokeOrRun(() => 
                    {
                        StatusMessage = $"加载字段出错: {ex.Message}";
                        LogError($"加载字段出错: {ex.Message}");
                    });
                }
            });
        }

        /// <summary>
        /// 清除日志
        /// </summary>
        private void ClearLog()
        {
            _logBuilder.Clear();
            LogContent = "";
        }

        /// <summary>
        /// 添加信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            AddLogEntry($"[信息] {message}");
        }

        /// <summary>
        /// 添加错误日志
        /// </summary>
        private void LogError(string message)
        {
            AddLogEntry($"[错误] {message}");
        }

        /// <summary>
        /// 添加警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            AddLogEntry($"[警告] {message}");
        }

        /// <summary>
        /// 添加日志条目
        /// </summary>
        private void AddLogEntry(string entry)
        {
            // 在日志构建器中添加条目
            _logBuilder.AppendLine($"{DateTime.Now:HH:mm:ss} {entry}");
            
            // 更新UI上的日志内容
            PresentationServices.UiThread.InvokeOrRun(() => 
            {
                LogContent = _logBuilder.ToString();
            });
        }
        
        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "按字段批量裁剪要素图层工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于按照指定字段对要素图层进行分组并导出为多个Shapefile文件。\n\n" +
                               "参数说明：\n" +
                               "1. 要素图层：选择要进行批量裁剪的要素图层\n" +
                               "2. 分组裁剪字段：用于分组的字段，每个不同的字段值将作为一个分组\n" +
                               "3. 输出文件夹：导出文件的存储位置\n" +
                               "4. 单独创建文件夹：是否为每个分组创建单独的文件夹\n\n" +
                               "操作步骤：\n" +
                               "1. 选择要素图层\n" +
                               "2. 选择分组裁剪字段\n" +
                               "3. 设置输出文件夹\n" +
                               "4. 选择是否为每个分组创建单独文件夹\n" +
                               "5. 点击运行按钮执行裁剪操作\n\n" +
                               "注意事项：\n" +
                               "- 导出文件名将使用分组值命名，空值时使用\"图层名_空值\"\n" +
                               "- 分组值中的特殊字符将被替换为合法的文件名字符\n" +
                               "- 操作过程和结果将显示在日志窗口中\n" +
                               "- 导出的图层不会自动添加到地图中\n" +
                               "- 处理过程中可以点击\"停止\"按钮取消操作";

            PresentationServices.Dialogs.Show(helpContent, "按字段批量裁剪要素图层工具使用说明");
        }
    }
}
