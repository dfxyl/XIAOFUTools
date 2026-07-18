using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;

namespace XIAOFUTools.Features.Editing.GroupNumbering
{
    internal partial class GroupNumberingViewModel
    {

        /// <summary>
        /// 刷新图层列表（供DockPane刷新按钮调用）
        /// </summary>
        public void RefreshLayers()
        {
            LoadLayersAsync();
        }

        /// <summary>
        /// 更新所选要素数量的提示信息
        /// </summary>
        private void UpdateSelectionInfo()
        {
            Task.Run(async () =>
            {
                int count = 0;
                await QueuedTask.Run(() =>
                {
                    if (_selectedLayer != null)
                    {
                        count = _selectedLayer.SelectionCount;
                    }
                });

                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    if (count > 0)
                    {
                        // 如果从无选择变为有选择，默认开启使用选择
                        if (!HasSelection && !_useSelection)
                            UseSelection = true;
                        HasSelection = true;
                        SelectedCount = count;
                        SelectionInfoText = $"已选 {count}";
                    }
                    else
                    {
                        HasSelection = false;
                        if (_useSelection)
                            UseSelection = false; // 无选择时强制关闭
                        SelectedCount = 0;
                        SelectionInfoText = "全部要素";
                    }
                });
            });
        }

        /// <summary>
        /// 地图选择变化事件
        /// </summary>
        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            // 简单处理：任意选择变化均尝试刷新显示
            UpdateSelectionInfo();
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "要素顺序编号工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于为要素添加编号，可根据指定字段进行分组编号。\n\n" +
                               "参数说明：\n" +
                               "1. 编号图层：选择要进行编号的图层\n" +
                               "2. 分组字段：用于对要素进行分组的字段，选择\"不分组\"则不分组\n" +
                               "3. 编号字段：用于存储生成的编号的字段\n" +
                               "4. 有效位数：编号的位数，例如选择3，则编号为001、002...\n" +
                               "5. 起始号码：编号的起始值，例如设置为5，则编号从005开始\n" +
                               "6. 前缀：编号前的文本，例如\"编号\"\n" +
                               "7. 后缀：编号后的文本，例如\"号\"\n" +
                               "8. 只编辑空记录：选中时只对编号字段为空的要素进行编号\n\n" +
                               "操作步骤：\n" +
                               "1. 选择需要编号的图层\n" +
                               "2. 选择分组字段（可选）\n" +
                               "3. 选择编号字段\n" +
                               "4. 设置编号参数（位数、起始号码、前后缀等）\n" +
                               "5. 点击执行按钮进行编号\n\n" +
                               "注意事项：\n" +
                               "- 编号字段必须是文本类型\n" +
                               "- 操作不可恢复，请确认后再执行";

            PresentationServices.Dialogs.Show(helpContent, "要素顺序编号工具使用说明");
        }
    }
}
