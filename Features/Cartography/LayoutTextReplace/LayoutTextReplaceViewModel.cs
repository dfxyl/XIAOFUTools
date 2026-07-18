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

namespace XIAOFUTools.Features.Cartography.LayoutTextReplace
{
    /// <summary>
    /// 布局元素查找替换视图模型
    /// </summary>
    public partial class LayoutTextReplaceViewModel : INotifyPropertyChanged
    {

        private string _searchText = string.Empty;
        private string _replaceSearchText = string.Empty;
        private string _replaceText = string.Empty;
        private int _selectedTabIndex = 0;
        private string _statusMessage = string.Empty;
        private TextElementResult _selectedSearchResult;
        private TextElementResult _selectedReplaceResult;

        public LayoutTextReplaceViewModel()
        {
            InitializeCommands();
            LoadLayouts();
        }
    }

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
}
