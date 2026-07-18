#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using XIAOFUTools.Features.Custom.GisToolPocket.Application;
using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Presentation
{
    public partial class SystemToolPickerWindow : Window, INotifyPropertyChanged
    {
        private readonly List<ArcGisCommandDefinition> _allCommands = new();
        private readonly List<SystemToolPickerItem> _allItems = new();
        private string _selectedCountText = "0 个已选项目";

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<SystemToolboxListItem> Toolboxes { get; } = new();

        public ObservableCollection<SystemToolPickerItem> VisibleTools { get; } = new();

        public List<SystemToolPickerItem> SelectedTools => _allItems.Where(item => item.IsSelected).ToList();

        public string SelectedCountText
        {
            get => _selectedCountText;
            private set
            {
                if (_selectedCountText == value)
                    return;

                _selectedCountText = value;
                OnPropertyChanged();
            }
        }

        public SystemToolPickerWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += SystemToolPickerWindow_Loaded;
        }

        private async void SystemToolPickerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetBusy(true, "正在加载 ArcGIS 工具目录...");
            try
            {
                var commands = await Task.Run(ToolboxApplicationService.LoadCommands);
                _allCommands.Clear();
                _allCommands.AddRange(commands);
                BuildItems();
                RebuildToolboxList();
                UpdateVisibleTools();
                UpdateSelectionSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ArcGIS 工具目录加载失败");
                DialogResult = false;
                Close();
            }
            finally
            {
                SetBusy(false, $"{_allItems.Count} 个可选项目");
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RebuildToolboxList();
            UpdateVisibleTools();
        }

        private void ToolboxList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateVisibleTools();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTools.Count == 0)
            {
                MessageBox.Show("至少选择一个项目。", "添加 ArcGIS 项目");
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BuildItems()
        {
            _allItems.Clear();
            foreach (var command in _allCommands)
            {
                var toolKind = command.Kind switch
                {
                    ArcGisCommandKind.MapTool => ToolboxApplicationService.MapToolKind,
                    ArcGisCommandKind.Command => ToolboxApplicationService.CommandToolKind,
                    _ => ToolboxApplicationService.ToolboxToolKind
                };

                var item = new SystemToolPickerItem
                {
                    ToolboxLabel = command.Source,
                    Alias = command.Source,
                    Name = command.Id,
                    Caption = command.Caption,
                    ToolPath = command.Id,
                    ToolKind = toolKind,
                    DisplayToolKind = toolKind switch
                    {
                        ToolboxApplicationService.MapToolKind => "地图工具",
                        ToolboxApplicationService.CommandToolKind => "命令",
                        _ => "地理处理"
                    }
                };
                item.PropertyChanged += ToolItem_PropertyChanged;
                _allItems.Add(item);
            }
        }

        private void ToolItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemToolPickerItem.IsSelected))
                UpdateSelectionSummary();
        }

        private void RebuildToolboxList()
        {
            var search = SearchBox.Text?.Trim() ?? string.Empty;
            var filtered = FilterItems(search);

            Toolboxes.Clear();
            Toolboxes.Add(new SystemToolboxListItem(string.Empty, "全部来源", filtered.Count));

            foreach (var group in filtered
                         .GroupBy(item => item.Alias)
                         .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                Toolboxes.Add(new SystemToolboxListItem(group.Key, group.Key, group.Count()));
            }

            if (ToolboxList.SelectedItem is not SystemToolboxListItem selected ||
                !Toolboxes.Any(item => item.Alias.Equals(selected.Alias, StringComparison.OrdinalIgnoreCase)))
            {
                ToolboxList.SelectedIndex = Toolboxes.Count > 0 ? 0 : -1;
            }
        }

        private void UpdateVisibleTools()
        {
            var search = SearchBox.Text?.Trim() ?? string.Empty;
            var filtered = FilterItems(search);
            var alias = (ToolboxList.SelectedItem as SystemToolboxListItem)?.Alias ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(alias))
                filtered = filtered.Where(item => item.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase)).ToList();

            VisibleTools.Clear();
            foreach (var item in filtered
                         .OrderBy(tool => tool.Caption, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase))
            {
                VisibleTools.Add(item);
            }

            StatusText.Text = $"{VisibleTools.Count} 个可选项目";
        }

        private List<SystemToolPickerItem> FilterItems(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return _allItems.ToList();

            return _allItems
                .Where(item => item.Caption.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                               item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                               item.Alias.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                               item.ToolKind.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                               item.DisplayToolKind.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
        }

        private void UpdateSelectionSummary()
        {
            SelectedCountText = $"{SelectedTools.Count} 个已选项目";
        }

        private void SetBusy(bool isBusy, string status)
        {
            SearchBox.IsEnabled = !isBusy;
            ToolboxList.IsEnabled = !isBusy;
            ToolGrid.IsEnabled = !isBusy;
            StatusText.Text = status;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class SystemToolPickerItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ToolboxLabel { get; set; } = string.Empty;

        public string Alias { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Caption { get; set; } = string.Empty;

        public string ToolPath { get; set; } = string.Empty;

        public string ToolKind { get; set; } = string.Empty;

        public string DisplayToolKind { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public sealed class SystemToolboxListItem
    {
        public SystemToolboxListItem(string alias, string displayText, int count)
        {
            Alias = alias;
            DisplayText = $"{displayText} · {count}";
        }

        public string Alias { get; }

        public string DisplayText { get; }
    }
}
