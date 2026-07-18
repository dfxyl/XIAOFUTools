using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using System.Threading;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace XIAOFUTools.Features.DataManagement.FieldCopyTool
{
    public partial class FieldCopyToolViewModel
    {

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            LogInfo("正在刷新图层列表...");
            LoadLayers();
        }

        /// <summary>
        /// 字段全选
        /// </summary>
        private void SelectAllFields()
        {
            foreach (var field in FieldList)
            {
                field.IsSelected = true;
            }
        }

        /// <summary>
        /// 字段反选
        /// </summary>
        private void InvertFieldSelection()
        {
            foreach (var field in FieldList)
            {
                field.IsSelected = !field.IsSelected;
            }
        }



        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "字段复制工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于将源数据图层的字段复制到目标图层中。\n" +
                               "支持Shapefile、File Geodatabase、Enterprise Geodatabase等多种数据源。\n\n" +
                               "参数说明：\n" +
                               "1. 源数据：选择要复制字段的源图层\n" +
                               "2. 字段列表：显示源图层的所有字段，可多选\n" +
                               "3. 目标图层：选择要添加字段的目标图层，可多选\n\n" +
                               "操作步骤：\n" +
                               "1. 选择源数据图层\n" +
                               "2. 在字段列表中勾选要复制的字段\n" +
                               "3. 在目标图层列表中勾选要添加字段的图层\n" +
                               "4. 点击开始按钮执行复制操作\n\n" +
                               "注意事项：\n" +
                               "- 如果目标图层中已存在同名字段，将跳过该字段\n" +
                               "- 系统字段（OBJECTID、SHAPE等）和几何字段不会显示在字段列表中\n" +
                               "- 不支持复制几何字段、OID字段和GlobalID字段\n" +
                               "- 字符串字段的默认长度为255\n" +
                               "- 操作过程和结果将显示在日志窗口中";

            PresentationServices.Dialogs.Show(helpContent, "字段复制工具使用说明");
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] {message}\n";
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] 警告: {message}\n";
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] 错误: {message}\n";
        }
    }
}
