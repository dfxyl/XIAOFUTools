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

namespace XIAOFUTools.Tools.LayoutTools.LayoutTextReplace
{
    /// <summary>
    /// 布局元素查找替换视图模型
    /// </summary>
    public class LayoutTextReplaceViewModel : INotifyPropertyChanged
    {
        #region 私有字段

        private string _searchText = string.Empty;
        private string _replaceSearchText = string.Empty;
        private string _replaceText = string.Empty;
        private int _selectedTabIndex = 0;
        private string _statusMessage = string.Empty;
        private TextElementResult _selectedSearchResult;
        private TextElementResult _selectedReplaceResult;

        #endregion

        #region 构造函数

        public LayoutTextReplaceViewModel()
        {
            InitializeCommands();
            LoadLayouts();
        }

        #endregion

        #region 属性

        /// <summary>
        /// 查找文本（查找标签页）
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 查找文本（替换标签页）
        /// </summary>
        public string ReplaceSearchText
        {
            get => _replaceSearchText;
            set { _replaceSearchText = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 替换文本
        /// </summary>
        public string ReplaceText
        {
            get => _replaceText;
            set { _replaceText = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 选中的标签页索引
        /// </summary>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { _selectedTabIndex = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 选中的查找结果
        /// </summary>
        public TextElementResult SelectedSearchResult
        {
            get => _selectedSearchResult;
            set { _selectedSearchResult = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 选中的替换结果
        /// </summary>
        public TextElementResult SelectedReplaceResult
        {
            get => _selectedReplaceResult;
            set { _selectedReplaceResult = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 查找结果数量
        /// </summary>
        public int SearchResultCount => SearchResults?.Count ?? 0;

        /// <summary>
        /// 替换结果数量
        /// </summary>
        public int ReplaceResultCount => ReplaceResults?.Count ?? 0;

        /// <summary>
        /// 布局列表
        /// </summary>
        public ObservableCollection<LayoutSelectItem> Layouts { get; } = new ObservableCollection<LayoutSelectItem>();

        /// <summary>
        /// 查找结果列表
        /// </summary>
        public ObservableCollection<TextElementResult> SearchResults { get; } = new ObservableCollection<TextElementResult>();

        /// <summary>
        /// 替换结果列表
        /// </summary>
        public ObservableCollection<TextElementResult> ReplaceResults { get; } = new ObservableCollection<TextElementResult>();

        #endregion

        #region 命令

        public ICommand SelectAllLayoutsCommand { get; private set; }
        public ICommand InvertLayoutSelectionCommand { get; private set; }
        public ICommand RefreshLayoutsCommand { get; private set; }
        public ICommand SearchAllCommand { get; private set; }
        public ICommand SearchForReplaceCommand { get; private set; }
        public ICommand ReplaceSelectedCommand { get; private set; }
        public ICommand ReplaceAllCommand { get; private set; }
        public ICommand NavigateToElementCommand { get; private set; }
        public ICommand NavigateToReplaceElementCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }

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

        #endregion

        #region 方法

        /// <summary>
        /// 加载布局列表
        /// </summary>
        private async void LoadLayouts()
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var project = Project.Current;
                    if (project == null) return;

                    var layouts = project.GetItems<LayoutProjectItem>();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Layouts.Clear();
                        foreach (var layout in layouts)
                        {
                            Layouts.Add(new LayoutSelectItem { Name = layout.Name, IsSelected = true });
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }

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
                    Application.Current.Dispatcher.Invoke(() =>
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
                                Application.Current.Dispatcher.Invoke(() =>
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

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "布局元素查找替换帮助");
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #region 辅助类

    /// <summary>
    /// 布局选择项
    /// </summary>
    public class LayoutSelectItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 文本元素查找结果
    /// </summary>
    public class TextElementResult : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _fullText;
        private string _searchKeyword;

        public string LayoutName { get; set; }
        public string ElementName { get; set; }

        /// <summary>
        /// 搜索关键词（用于高亮显示）
        /// </summary>
        public string SearchKeyword
        {
            get => _searchKeyword;
            set
            {
                _searchKeyword = value;
                OnPropertyChanged();
            }
        }

        public string FullText
        {
            get => _fullText;
            set
            {
                _fullText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        /// <summary>
        /// 显示文本（截断超长内容）
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (string.IsNullOrEmpty(FullText)) return string.Empty;
                return FullText.Length > 50 ? FullText.Substring(0, 50) + "..." : FullText;
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 元素引用
        /// </summary>
        public Element ElementReference { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 简单的命令实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
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

    /// <summary>
    /// 高亮文本控件 - 用于在文本中高亮显示搜索关键词
    /// </summary>
    public class HighlightTextBlock : System.Windows.Controls.TextBlock
    {
        public static readonly System.Windows.DependencyProperty HighlightTextProperty =
            System.Windows.DependencyProperty.Register(
                nameof(HighlightText),
                typeof(string),
                typeof(HighlightTextBlock),
                new System.Windows.PropertyMetadata(string.Empty, OnHighlightChanged));

        public static readonly System.Windows.DependencyProperty SourceTextProperty =
            System.Windows.DependencyProperty.Register(
                nameof(SourceText),
                typeof(string),
                typeof(HighlightTextBlock),
                new System.Windows.PropertyMetadata(string.Empty, OnHighlightChanged));

        public static readonly System.Windows.DependencyProperty HighlightBrushProperty =
            System.Windows.DependencyProperty.Register(
                nameof(HighlightBrush),
                typeof(System.Windows.Media.Brush),
                typeof(HighlightTextBlock),
                new System.Windows.PropertyMetadata(System.Windows.Media.Brushes.Yellow, OnHighlightChanged));

        /// <summary>
        /// 要高亮的文本
        /// </summary>
        public string HighlightText
        {
            get => (string)GetValue(HighlightTextProperty);
            set => SetValue(HighlightTextProperty, value);
        }

        /// <summary>
        /// 源文本
        /// </summary>
        public string SourceText
        {
            get => (string)GetValue(SourceTextProperty);
            set => SetValue(SourceTextProperty, value);
        }

        /// <summary>
        /// 高亮背景色
        /// </summary>
        public System.Windows.Media.Brush HighlightBrush
        {
            get => (System.Windows.Media.Brush)GetValue(HighlightBrushProperty);
            set => SetValue(HighlightBrushProperty, value);
        }

        private static void OnHighlightChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (d is HighlightTextBlock textBlock)
            {
                textBlock.UpdateHighlight();
            }
        }

        private void UpdateHighlight()
        {
            Inlines.Clear();

            var sourceText = SourceText ?? string.Empty;
            var highlightText = HighlightText ?? string.Empty;

            if (string.IsNullOrEmpty(sourceText))
                return;

            if (string.IsNullOrEmpty(highlightText))
            {
                Inlines.Add(new System.Windows.Documents.Run(sourceText));
                return;
            }

            int currentIndex = 0;
            int matchIndex;

            while ((matchIndex = sourceText.IndexOf(highlightText, currentIndex, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                // 添加匹配前的普通文本
                if (matchIndex > currentIndex)
                {
                    Inlines.Add(new System.Windows.Documents.Run(sourceText.Substring(currentIndex, matchIndex - currentIndex)));
                }

                // 添加高亮文本
                var highlightRun = new System.Windows.Documents.Run(sourceText.Substring(matchIndex, highlightText.Length))
                {
                    Background = HighlightBrush,
                    FontWeight = System.Windows.FontWeights.Bold
                };
                Inlines.Add(highlightRun);

                currentIndex = matchIndex + highlightText.Length;
            }

            // 添加剩余的普通文本
            if (currentIndex < sourceText.Length)
            {
                Inlines.Add(new System.Windows.Documents.Run(sourceText.Substring(currentIndex)));
            }
        }
    }

    #endregion
}
