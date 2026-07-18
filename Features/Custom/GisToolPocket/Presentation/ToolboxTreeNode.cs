#nullable enable

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

using XIAOFUTools.Features.Custom.GisToolPocket.Application;
using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Presentation
{
    public sealed class ToolboxTreeNode : System.ComponentModel.INotifyPropertyChanged
    {
        public const string CatalogKind = "Catalog";
        public const string ToolsetKind = "Toolset";
        public const string ToolKind = "Tool";

        private string _editableName = string.Empty;
        private bool _isExpanded = true;

        private readonly Func<bool>? _delete;
        private readonly Action<string> _rename;
        private readonly Func<bool> _getVisible;
        private readonly Action<bool> _setVisible;
        private readonly Func<string> _detailPath;
        private readonly Func<string> _nodeKey;

        private ToolboxTreeNode(
            string kind,
            string internalName,
            Func<string> nodeKey,
            Func<string> detailPath,
            Action<string> rename,
            Func<bool> getVisible,
            Action<bool> setVisible,
            Func<bool>? delete = null)
        {
            Kind = kind;
            InternalName = internalName;
            _nodeKey = nodeKey;
            _detailPath = detailPath;
            _rename = rename;
            _getVisible = getVisible;
            _setVisible = setVisible;
            _delete = delete;
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ToolboxTreeNode> Children { get; } = new();

        public string Kind { get; }

        public string InternalName { get; }

        public ToolboxCatalog? OwnerCatalog { get; private set; }

        public ToolboxToolset? Toolset { get; private set; }

        public ToolboxTool? Tool { get; private set; }

        public ToolboxToolset? ParentToolset { get; private set; }

        public ToolboxTreeNode? ParentNode { get; private set; }

        public string NodeKey => _nodeKey();

        public string FullPath => _detailPath();

        public bool CanDelete => _delete is not null;

        public bool CanRename => true;

        public bool IsVisible => _getVisible();

        public string VisibilityGlyph => IsVisible ? "👁" : "⊘";

        public string VisibilityTooltip => IsVisible ? "当前显示在菜单中" : "当前不显示在菜单中";

        public double NodeOpacity => IsVisible ? 1.0 : 0.45;

        public bool CanAddTool => IsCustomOwner &&
                                  (Kind == CatalogKind || Kind == ToolsetKind || Kind == ToolKind);

        public bool CanAddToolset => IsCustomOwner &&
                                     (Kind == CatalogKind || Kind == ToolsetKind || Kind == ToolKind);

        public bool CanDrag => OwnerCatalog is not null &&
                               ToolboxApplicationService.IsCustomCatalog(OwnerCatalog) &&
                               Kind != CatalogKind;

        private bool IsCustomOwner => OwnerCatalog is not null &&
                                      ToolboxApplicationService.IsCustomCatalog(OwnerCatalog);

        public string EditableName
        {
            get => _editableName;
            set
            {
                if (_editableName == value)
                    return;

                _editableName = value;
                _rename(value);
                OnPropertyChanged(nameof(EditableName));
                OnPropertyChanged(nameof(NodeKey));
                OnPropertyChanged(nameof(FullPath));
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;

                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public string Badge => Kind switch
        {
            CatalogKind => "箱",
            ToolsetKind => "集",
            _ => Tool?.ToolKind switch
            {
                var kind when string.Equals(kind, ToolboxApplicationService.CommandToolKind, StringComparison.OrdinalIgnoreCase) => "命",
                var kind when string.Equals(kind, ToolboxApplicationService.MapToolKind, StringComparison.OrdinalIgnoreCase) => "绘",
                _ => "工"
            }
        };

        public Brush BadgeBrush => Kind switch
        {
            CatalogKind => new SolidColorBrush(Color.FromRgb(0, 121, 193)),
            ToolsetKind => new SolidColorBrush(Color.FromRgb(32, 137, 111)),
            _ => Tool?.ToolKind switch
            {
                var kind when string.Equals(kind, ToolboxApplicationService.CommandToolKind, StringComparison.OrdinalIgnoreCase) => new SolidColorBrush(Color.FromRgb(0, 121, 193)),
                var kind when string.Equals(kind, ToolboxApplicationService.MapToolKind, StringComparison.OrdinalIgnoreCase) => new SolidColorBrush(Color.FromRgb(77, 139, 49)),
                _ => new SolidColorBrush(Color.FromRgb(116, 90, 180))
            }
        };

        public string KindText => Kind switch
        {
            CatalogKind => "工具箱",
            ToolsetKind => "工具集",
            _ => Tool?.ToolKind switch
            {
                var kind when string.Equals(kind, ToolboxApplicationService.CommandToolKind, StringComparison.OrdinalIgnoreCase) => "命令",
                var kind when string.Equals(kind, ToolboxApplicationService.MapToolKind, StringComparison.OrdinalIgnoreCase) => "地图工具",
                _ => "地理处理"
            }
        };

        public static ToolboxTreeNode FromCatalog(ToolboxCatalog catalog)
        {
            var node = new ToolboxTreeNode(
                CatalogKind,
                catalog.SourceFileName,
                () => catalog.ToolboxPath,
                () => catalog.ToolboxPath,
                value => catalog.DisplayName = value,
                () => catalog.IsVisible,
                value => catalog.IsVisible = value)
            {
                EditableName = catalog.DisplayTitle
            };
            node.OwnerCatalog = catalog;

            foreach (var toolset in catalog.Toolsets)
                AddToolsetToParent(node, toolset, flattenDefaultToolset: true, storageParent: null);

            return node;
        }

        private static void AddToolsetToParent(ToolboxTreeNode parent, ToolboxToolset toolset, bool flattenDefaultToolset, ToolboxToolset? storageParent)
        {
            if (flattenDefaultToolset && ToolboxApplicationService.IsDefaultToolsetName(toolset.Name))
            {
                foreach (var child in toolset.Children)
                    AddToolsetToParent(parent, child, flattenDefaultToolset: false, storageParent: toolset);

                foreach (var tool in toolset.Tools)
                    parent.Children.Add(FromTool(tool, parent, toolset));

                return;
            }

            parent.Children.Add(FromToolset(toolset, parent, storageParent));
        }

        private static ToolboxTreeNode FromToolset(ToolboxToolset toolset, ToolboxTreeNode parent, ToolboxToolset? storageParent)
        {
            var canDelete = parent.OwnerCatalog is not null && ToolboxApplicationService.IsCustomCatalog(parent.OwnerCatalog);
            var node = new ToolboxTreeNode(
                ToolsetKind,
                toolset.Name,
                () => $"{parent.NodeKey}\\{toolset.Name}",
                () => $"{parent.NodeKey}\\{toolset.Name}",
                value => toolset.Name = value,
                () => toolset.IsVisible,
                value => toolset.IsVisible = value,
                canDelete
                    ? () => storageParent is not null
                        ? storageParent.Children.Remove(toolset)
                        : parent.OwnerCatalog?.Toolsets.Remove(toolset) == true
                    : null)
            {
                EditableName = toolset.Name
            };
            node.OwnerCatalog = parent.OwnerCatalog;
            node.Toolset = toolset;
            node.ParentToolset = storageParent;
            node.ParentNode = parent;

            foreach (var child in toolset.Children)
                AddToolsetToParent(node, child, flattenDefaultToolset: false, storageParent: toolset);

            foreach (var tool in toolset.Tools)
                node.Children.Add(FromTool(tool, node, toolset));

            return node;
        }

        private static ToolboxTreeNode FromTool(ToolboxTool tool, ToolboxTreeNode parent, ToolboxToolset storageParent)
        {
            var canDelete = parent.OwnerCatalog is not null && ToolboxApplicationService.IsCustomCatalog(parent.OwnerCatalog);
            var node = new ToolboxTreeNode(
                ToolKind,
                tool.Name,
                () => $"{parent.NodeKey}\\{tool.Name}",
                () => tool.ToolPath,
                value => tool.Caption = value,
                () => tool.IsVisible,
                value => tool.IsVisible = value,
                canDelete ? () => storageParent.Tools.Remove(tool) : null)
            {
                EditableName = string.IsNullOrWhiteSpace(tool.Caption) ? tool.Name : tool.Caption
            };
            node.OwnerCatalog = parent.OwnerCatalog;
            node.Tool = tool;
            node.ParentToolset = storageParent;
            node.ParentNode = parent;
            return node;
        }

        public bool Delete()
        {
            return _delete?.Invoke() == true;
        }

        public void ToggleVisible()
        {
            _setVisible(!IsVisible);
            OnPropertyChanged(nameof(IsVisible));
            OnPropertyChanged(nameof(VisibilityGlyph));
            OnPropertyChanged(nameof(VisibilityTooltip));
            OnPropertyChanged(nameof(NodeOpacity));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class ToolboxVisibilityGlyphConverter : IValueConverter
    {
        public static ToolboxVisibilityGlyphConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool visible && visible ? "👁" : "⊘";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class ToolboxVisibilityTooltipConverter : IValueConverter
    {
        public static ToolboxVisibilityTooltipConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool visible && visible ? "当前显示在菜单中" : "当前不显示在菜单中";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
