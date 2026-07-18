using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Cartography.LayoutTextReplace
{
    public partial class LayoutTextReplaceViewModel
    {

        /// <summary>
        /// 刷新布局列表
        /// </summary>
        private void RefreshLayouts()
        {
            LoadLayouts();
            StatusMessage = "布局列表已刷新";
        }

        /// <summary>
        /// 全选布局
        /// </summary>
        private void SelectAllLayouts()
        {
            foreach (var layout in Layouts)
            {
                layout.IsSelected = true;
            }
        }

        /// <summary>
        /// 反选布局
        /// </summary>
        private void InvertLayoutSelection()
        {
            foreach (var layout in Layouts)
            {
                layout.IsSelected = !layout.IsSelected;
            }
        }

        /// <summary>
        /// 替换所选
        /// </summary>
        private async Task ReplaceSelectedAsync()
        {
            var selectedItems = ReplaceResults.Where(r => r.IsSelected).ToList();
            if (!selectedItems.Any())
            {
                StatusMessage = "请选择要替换的项目";
                return;
            }

            if (string.IsNullOrEmpty(ReplaceSearchText))
            {
                StatusMessage = "请输入查找内容";
                return;
            }

            StatusMessage = "正在替换...";
            int replacedCount = 0;

            await QueuedTask.Run(() =>
            {
                foreach (var item in selectedItems)
                {
                    if (item.ElementReference is TextElement textElement)
                    {
                        try
                        {
                            var textProps = textElement.TextProperties;
                            if (textProps != null)
                            {
                                var newText = textProps.Text.Replace(ReplaceSearchText, ReplaceText);
                                textProps.Text = newText;
                                textElement.SetTextProperties(textProps);
                                
                                // 更新结果中的文本
                                PresentationServices.UiThread.InvokeOrRun(() =>
                                {
                                    item.FullText = newText;
                                });
                                replacedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"替换失败: {ex.Message}");
                        }
                    }
                }
            });

            // 刷新结果列表
            await SearchForReplaceAsync();
            StatusMessage = $"替换完成，共替换 {replacedCount} 处";
        }

        /// <summary>
        /// 打开布局窗格
        /// </summary>
        private async Task<ILayoutPane> OpenLayoutPaneAsync(LayoutProjectItem layoutItem)
        {
            // 获取布局对象
            ArcGIS.Desktop.Layouts.Layout layout = null;
            string layoutName = layoutItem.Name;
            
            await QueuedTask.Run(() =>
            {
                layout = layoutItem.GetLayout();
            });

            if (layout == null) 
                return null;

            System.Diagnostics.Debug.WriteLine($"[LayoutTextReplace] 目标布局: {layoutName}");

            // 遍历窗格，使用窗格的 Caption（标题）进行匹配
            ILayoutPane existingPane = null;
            foreach (var pane in ProApp.Panes)
            {
                if (pane is ILayoutPane lp)
                {
                    // 获取窗格的标题（Caption）
                    var paneAsBase = pane as ArcGIS.Desktop.Framework.Contracts.Pane;
                    var paneCaption = paneAsBase?.Caption ?? "";
                    
                    System.Diagnostics.Debug.WriteLine($"[LayoutTextReplace] 检查窗格 Caption: '{paneCaption}'");
                    
                    // 布局窗格的标题通常就是布局名称
                    if (paneCaption == layoutName)
                    {
                        existingPane = lp;
                        System.Diagnostics.Debug.WriteLine($"[LayoutTextReplace] 找到匹配的已打开窗格!");
                        break;
                    }
                }
            }

            if (existingPane != null)
            {
                // 激活已存在的窗格
                System.Diagnostics.Debug.WriteLine($"[LayoutTextReplace] 激活已存在的窗格");
                (existingPane as ArcGIS.Desktop.Framework.Contracts.Pane)?.Activate();
                return existingPane;
            }

            // 如果没有打开，则创建新的布局窗格
            System.Diagnostics.Debug.WriteLine($"[LayoutTextReplace] 未找到已打开的窗格，创建新窗格");
            return await ProApp.Panes.CreateLayoutPaneAsync(layout);
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "布局元素查找替换工具帮助\n\n" +
                "功能描述：\n" +
                "在布局中批量查找和替换文本元素内容，类似Excel的查找替换功能。\n\n" +
                "【查找】标签页：\n" +
                "1. 输入查找内容\n" +
                "2. 选择要搜索的布局范围\n" +
                "3. 点击\"查找全部\"按钮\n" +
                "4. 双击结果可跳转到对应元素位置\n\n" +
                "【替换】标签页：\n" +
                "1. 输入查找内容和替换内容\n" +
                "2. 选择要搜索的布局范围\n" +
                "3. 点击\"查找全部\"查看匹配结果\n" +
                "4. 勾选要替换的项目\n" +
                "5. 点击\"替换所选\"或\"全部替换\"\n\n" +
                "注意事项：\n" +
                "- 替换操作会直接修改布局内容，请谨慎操作\n" +
                "- 建议在替换前保存项目\n" +
                "- 支持在文本元素和组元素中查找";

            PresentationServices.Dialogs.Show(helpContent, "布局元素查找替换帮助");
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
