#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.General.QuickAddData
{
    internal sealed class QuickDataTreeItemViewModel : PropertyChangedBase
    {
        private bool _isSelected;
        private bool _isExpanded;

        public QuickDataTreeItemViewModel(QuickDataTreeNode treeNode, QuickDataTreeItemViewModel? parent = null)
        {
            Parent = parent;
            DisplayName = treeNode.DisplayName;
            NodeKind = treeNode.NodeKind;
            BucketKind = treeNode.BucketKind;
            Group = treeNode.Group;
            DataNode = treeNode.DataNode;
            DetailText = string.Empty;
            ToolTipText = BuildToolTipText();
            TypeDisplayText = BuildTypeDisplayText();
            IconGlyph = BuildIconGlyph();
            Children = new ObservableCollection<QuickDataTreeItemViewModel>(
                treeNode.Children.Select(child => new QuickDataTreeItemViewModel(child, this)));
            StateKey = QuickDataTreeStateKeyBuilder.Build(NodeKind, DisplayName, Group, DataNode, BucketKind, Parent?.StateKey ?? string.Empty);
            IsExpanded = NodeKind == QuickDataTreeNodeKind.Group;
        }

        public QuickDataTreeItemViewModel? Parent { get; }

        public string DisplayName { get; }

        public string DetailText { get; }

        public string ToolTipText { get; }

        public string TypeDisplayText { get; }

        public string IconGlyph { get; }

        public QuickDataTreeNodeKind NodeKind { get; }

        public QuickDataBucketKind? BucketKind { get; }

        public QuickDataGroup? Group { get; }

        public QuickDataNode? DataNode { get; }

        public ObservableCollection<QuickDataTreeItemViewModel> Children { get; }

        public string StateKey { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsGroupNode => NodeKind == QuickDataTreeNodeKind.Group;

        public bool IsTopLevelDataNode => NodeKind == QuickDataTreeNodeKind.DataNode
            && Parent?.NodeKind == QuickDataTreeNodeKind.Group;

        public bool IsMapDraggable => IsGroupNode || (NodeKind == QuickDataTreeNodeKind.DataNode && DataNode != null);

        public bool IsTreeReorderable => IsGroupNode || IsTopLevelDataNode;

        public Visibility AddFileToGroupVisibility => IsGroupNode ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ImportToGroupVisibility => IsGroupNode ? Visibility.Visible : Visibility.Collapsed;

        public Visibility LoadGroupVisibility => IsGroupNode ? Visibility.Visible : Visibility.Collapsed;

        public Visibility RenameGroupVisibility => IsGroupNode ? Visibility.Visible : Visibility.Collapsed;

        public Visibility LoadBucketVisibility => NodeKind == QuickDataTreeNodeKind.TypeBucket ? Visibility.Visible : Visibility.Collapsed;

        public Visibility LoadNodeVisibility => NodeKind == QuickDataTreeNodeKind.DataNode ? Visibility.Visible : Visibility.Collapsed;

        public Visibility RefreshDatabaseVisibility =>
            NodeKind == QuickDataTreeNodeKind.DataNode && DataNode?.NodeKind == QuickDataNodeKind.Geodatabase
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility DeleteVisibility =>
            NodeKind == QuickDataTreeNodeKind.Group || NodeKind == QuickDataTreeNodeKind.DataNode
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string LoadMenuHeader
        {
            get
            {
                if (NodeKind == QuickDataTreeNodeKind.TypeBucket)
                {
                    return "加载本类";
                }

                if (NodeKind == QuickDataTreeNodeKind.DataNode && DataNode?.NodeKind == QuickDataNodeKind.Geodatabase)
                {
                    return "加载数据库子项";
                }

                if (NodeKind == QuickDataTreeNodeKind.DataNode && DataNode?.NodeKind == QuickDataNodeKind.FeatureDataset)
                {
                    return "加载要素数据集";
                }

                return "加载到当前地图";
            }
        }

        public QuickDataGroup? ResolveGroup()
        {
            return Group ?? Parent?.ResolveGroup();
        }

        private string BuildToolTipText()
        {
            if (NodeKind == QuickDataTreeNodeKind.Group)
            {
                return $"分组: {DisplayName}";
            }

            if (NodeKind == QuickDataTreeNodeKind.TypeBucket)
            {
                return $"分类: {DisplayName}";
            }

            if (DataNode == null)
            {
                return DisplayName;
            }

            var lines = new[]
            {
                $"名称: {DataNode.Name}",
                $"类型: {GetNodeKindText(DataNode)}",
                $"坐标系: {DataNode.CoordinateSystem}",
                $"路径: {DataNode.SourcePath}"
            };

            return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        private string BuildTypeDisplayText()
        {
            if (NodeKind != QuickDataTreeNodeKind.DataNode || DataNode == null)
            {
                return string.Empty;
            }

            return $" [{GetNodeKindText(DataNode)}]";
        }

        private string BuildIconGlyph()
        {
            if (NodeKind == QuickDataTreeNodeKind.Group)
            {
                return "📁";
            }

            if (NodeKind == QuickDataTreeNodeKind.TypeBucket)
            {
                return "🗂";
            }

            if (DataNode == null)
            {
                return "•";
            }

            return DataNode.NodeKind switch
            {
                QuickDataNodeKind.Geodatabase => "🗄",
                QuickDataNodeKind.FeatureDataset => "🧩",
                QuickDataNodeKind.Table => "▦",
                QuickDataNodeKind.Raster => "▧",
                QuickDataNodeKind.LayerFile => "◫",
                _ when DataNode.GeometryKind == QuickDataGeometryKind.Point => "●",
                _ when DataNode.GeometryKind == QuickDataGeometryKind.Polyline => "／",
                _ when DataNode.GeometryKind == QuickDataGeometryKind.Polygon => "⬒",
                _ when DataNode.GeometryKind == QuickDataGeometryKind.Multipoint => "∶",
                _ => "◻"
            };
        }

        private static string GetNodeKindText(QuickDataNode node)
        {
            return node.NodeKind switch
            {
                QuickDataNodeKind.Geodatabase => "数据库",
                QuickDataNodeKind.FeatureDataset => "要素数据集",
                QuickDataNodeKind.Table => "表",
                QuickDataNodeKind.Raster => "栅格",
                QuickDataNodeKind.LayerFile => "图层文件",
                _ when node.GeometryKind == QuickDataGeometryKind.Point => "点",
                _ when node.GeometryKind == QuickDataGeometryKind.Polyline => "线",
                _ when node.GeometryKind == QuickDataGeometryKind.Polygon => "面",
                _ when node.GeometryKind == QuickDataGeometryKind.Multipoint => "多点",
                _ => "要素类"
            };
        }
    }
}
