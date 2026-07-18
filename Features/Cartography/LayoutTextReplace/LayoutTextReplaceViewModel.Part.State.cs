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
    public partial class LayoutTextReplaceViewModel
    {

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

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
