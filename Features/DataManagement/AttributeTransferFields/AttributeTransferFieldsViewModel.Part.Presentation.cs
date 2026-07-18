using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.DataManagement.AttributeTransferFields
{
    public partial class AttributeTransferFieldsViewModel
    {
        private void FieldMappings_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var it in e.NewItems)
                {
                    if (it is FieldMappingItem fm)
                        fm.PropertyChanged += MappingItem_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (var it in e.OldItems)
                {
                    if (it is FieldMappingItem fm)
                        fm.PropertyChanged -= MappingItem_PropertyChanged;
                }
            }
            RaiseAllCanExecutes();
        }
        private void RemoveSelectedMappings()
        {
            var toRemove = FieldMappings.Where(m => m.IsSelected).ToList();
            foreach (var m in toRemove) FieldMappings.Remove(m);
        }
        private void ShowHelp()
        {
            string help =
                "属性传递[字段] 使用说明\n\n" +
                "功能：\n" +
                "在主/从两个数据集之间，根据关联键字段进行记录匹配，并按字段映射批量传递属性值。\n\n" +
                "步骤：\n" +
                "1) 选择主数据集与从数据集（支持要素图层与独立表）。\n" +
                "2) 分别选择主键字段与从键字段，用于匹配记录。\n" +
                "3) 通过自动匹配或手动添加，建立‘主字段 → 从字段’映射列表。\n" +
                "4) 选择传递方向（主→从 或 从→主）。\n" +
                "5) 点击开始执行，查看日志输出。\n\n" +
                "说明：\n" +
                "- 键值大小写与前后空格会被标准化再匹配。\n" +
                "- 数值类型之间可互转，任意类型可传到字符串字段。\n" +
                "- 若源数据存在重复键，将以首次出现的记录为准并给出警告。\n" +
                "- 可随时点击停止以取消正在进行的操作。";

            PresentationServices.Dialogs.Show(help, "属性传递[字段] 使用说明");
        }

        private void ShowLog()
        {
            var text = string.IsNullOrWhiteSpace(LogText) ? "暂无日志" : LogText;
            PresentationServices.Dialogs.Show(text, "执行日志");
        }
        private static bool IsSystemOrNonWritable(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            return _blockedNames.Contains(name);
        }
        private void LogInfo(string msg) => AppendLog($"[信息] {msg}");
        private void LogWarning(string msg) => AppendLog($"[警告] {msg}");
        private void LogError(string msg) => AppendLog($"[错误] {msg}");
        private void AppendLog(string msg)
        {
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogText += (string.IsNullOrEmpty(LogText) ? string.Empty : "\n") + msg;
            });
        }
    }
}
