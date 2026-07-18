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

        private void InitializeCommands()
        {
            SelectAllLayoutsCommand = new RelayCommand(SelectAllLayouts);
            InvertLayoutSelectionCommand = new RelayCommand(InvertLayoutSelection);
            RefreshLayoutsCommand = new RelayCommand(RefreshLayouts);
            SearchAllCommand = new RelayCommand(async () => await SearchAllAsync());
            SearchForReplaceCommand = new RelayCommand(async () => await SearchForReplaceAsync());
            ReplaceSelectedCommand = new RelayCommand(async () => await ReplaceSelectedAsync());
            ReplaceAllCommand = new RelayCommand(async () => await ReplaceAllAsync());
            NavigateToElementCommand = new RelayCommand(async () => await NavigateToElementAsync(SelectedSearchResult));
            NavigateToReplaceElementCommand = new RelayCommand(async () => await NavigateToElementAsync(SelectedReplaceResult));
            ShowHelpCommand = new RelayCommand(ShowHelp);
        }

        /// <summary>
        /// 查找全部（查找标签页）
        /// </summary>
        private async Task SearchAllAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                StatusMessage = "请输入查找内容";
                return;
            }

            var selectedLayouts = Layouts.Where(l => l.IsSelected).Select(l => l.Name).ToList();
            if (!selectedLayouts.Any())
            {
                StatusMessage = "请至少选择一个布局";
                return;
            }

            StatusMessage = "正在查找...";
            SearchResults.Clear();

            await QueuedTask.Run(() =>
            {
                var project = Project.Current;
                if (project == null) return;

                foreach (var layoutName in selectedLayouts)
                {
                    var layoutItem = project.GetItems<LayoutProjectItem>().FirstOrDefault(l => l.Name == layoutName);
                    if (layoutItem == null) continue;

                    var layout = layoutItem.GetLayout();
                    if (layout == null) continue;

                    SearchTextElementsInLayout(layout, SearchText, SearchResults);
                }
            });

            OnPropertyChanged(nameof(SearchResultCount));
            StatusMessage = $"查找完成，共找到 {SearchResultCount} 个结果";
        }

        /// <summary>
        /// 查找全部（替换标签页）
        /// </summary>
        private async Task SearchForReplaceAsync()
        {
            if (string.IsNullOrWhiteSpace(ReplaceSearchText))
            {
                StatusMessage = "请输入查找内容";
                return;
            }

            var selectedLayouts = Layouts.Where(l => l.IsSelected).Select(l => l.Name).ToList();
            if (!selectedLayouts.Any())
            {
                StatusMessage = "请至少选择一个布局";
                return;
            }

            StatusMessage = "正在查找...";
            ReplaceResults.Clear();

            await QueuedTask.Run(() =>
            {
                var project = Project.Current;
                if (project == null) return;

                foreach (var layoutName in selectedLayouts)
                {
                    var layoutItem = project.GetItems<LayoutProjectItem>().FirstOrDefault(l => l.Name == layoutName);
                    if (layoutItem == null) continue;

                    var layout = layoutItem.GetLayout();
                    if (layout == null) continue;

                    SearchTextElementsInLayout(layout, ReplaceSearchText, ReplaceResults);
                }
            });

            // 默认全选结果
            foreach (var result in ReplaceResults)
            {
                result.IsSelected = true;
            }

            OnPropertyChanged(nameof(ReplaceResultCount));
            StatusMessage = $"查找完成，共找到 {ReplaceResultCount} 个结果";
        }

        /// <summary>
        /// 在布局中搜索文本元素
        /// </summary>
        private void SearchTextElementsInLayout(ArcGIS.Desktop.Layouts.Layout layout, string searchText, ObservableCollection<TextElementResult> results)
        {
            var elements = layout.GetElements();

            foreach (var element in elements)
            {
                SearchTextElementsRecursive(layout.Name, element, searchText, results);
            }
        }

        /// <summary>
        /// 递归搜索文本元素
        /// </summary>
        private void SearchTextElementsRecursive(string layoutName, Element element, string searchText, ObservableCollection<TextElementResult> results)
        {
            // 检查是否是文本元素
            if (element is TextElement textElement)
            {
                var text = textElement.TextProperties?.Text ?? string.Empty;
                if (text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        results.Add(new TextElementResult
                        {
                            LayoutName = layoutName,
                            ElementName = element.Name,
                            FullText = text,
                            SearchKeyword = searchText,
                            ElementReference = textElement,
                            IsSelected = false
                        });
                    });
                }
            }

            // 检查是否是组元素，递归搜索子元素
            if (element is GroupElement groupElement)
            {
                foreach (var childElement in groupElement.Elements)
                {
                    SearchTextElementsRecursive(layoutName, childElement, searchText, results);
                }
            }
        }

        /// <summary>
        /// 全部替换
        /// </summary>
        private async Task ReplaceAllAsync()
        {
            if (!ReplaceResults.Any())
            {
                StatusMessage = "请先查找要替换的内容";
                return;
            }

            if (string.IsNullOrEmpty(ReplaceSearchText))
            {
                StatusMessage = "请输入查找内容";
                return;
            }

            // 选中所有结果
            foreach (var result in ReplaceResults)
            {
                result.IsSelected = true;
            }

            await ReplaceSelectedAsync();
        }

        /// <summary>
        /// 导航到元素
        /// </summary>
        private async Task NavigateToElementAsync(TextElementResult result)
        {
            if (result == null) return;

            try
            {
                var project = Project.Current;
                if (project == null) return;

                var layoutItem = project.GetItems<LayoutProjectItem>().FirstOrDefault(l => l.Name == result.LayoutName);
                if (layoutItem == null) return;

                // 打开布局视图（必须在主线程调用）
                ILayoutPane layoutPane = await OpenLayoutPaneAsync(layoutItem);
                if (layoutPane?.LayoutView == null) return;

                // 缩放和选中元素需要在 QueuedTask 中执行
                await QueuedTask.Run(() =>
                {
                    if (result.ElementReference != null)
                    {
                        var envelope = result.ElementReference.GetBounds();
                        if (envelope != null)
                        {
                            // 扩展范围以便更好地查看
                            var expandedEnvelope = envelope.Expand(1.5, 1.5, true);
                            layoutPane.LayoutView.ZoomTo(expandedEnvelope);

                            // 选中元素
                            layoutPane.LayoutView.SelectElement(result.ElementReference);
                        }
                    }
                });

                StatusMessage = $"已定位到: {result.ElementName}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导航到元素失败: {ex.Message}");
                StatusMessage = $"导航失败: {ex.Message}";
            }
        }
    }
}
