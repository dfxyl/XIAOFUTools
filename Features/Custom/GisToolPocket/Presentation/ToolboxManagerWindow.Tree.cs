#nullable enable

using ArcGIS.Desktop.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using XIAOFUTools.Features.Custom.GisToolPocket.Application;
using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Presentation
{
    public partial class ToolboxManagerWindow : Window
    {
        private static DropPlacement DetermineDropPlacement(ToolboxTreeNode targetNode, FrameworkElement container, Point position)
        {
            if (targetNode.Kind == ToolboxTreeNode.CatalogKind)
                return DropPlacement.Inside;

            if (targetNode.Kind == ToolboxTreeNode.ToolKind)
                return position.Y < container.ActualHeight / 2 ? DropPlacement.Before : DropPlacement.After;

            if (position.Y < container.ActualHeight * 0.28)
                return DropPlacement.Before;

            if (position.Y > container.ActualHeight * 0.72)
                return DropPlacement.After;

            return DropPlacement.Inside;
        }

        private static bool CanDropNode(ToolboxTreeNode source, ToolboxTreeNode target, DropPlacement placement)
        {
            if (!source.CanDrag ||
                source.OwnerCatalog is null ||
                target.OwnerCatalog is null ||
                !ToolboxApplicationService.IsCustomCatalog(source.OwnerCatalog) ||
                !ToolboxApplicationService.IsCustomCatalog(target.OwnerCatalog) ||
                ReferenceEquals(source, target))
            {
                return false;
            }

            if (IsAncestor(source, target))
                return false;

            if (source.Kind == ToolboxTreeNode.ToolKind)
            {
                return target.Kind switch
                {
                    ToolboxTreeNode.CatalogKind => true,
                    ToolboxTreeNode.ToolsetKind => true,
                    ToolboxTreeNode.ToolKind => target.ParentToolset is not null,
                    _ => false
                };
            }

            if (source.Kind == ToolboxTreeNode.ToolsetKind)
            {
                return target.Kind switch
                {
                    ToolboxTreeNode.CatalogKind => true,
                    ToolboxTreeNode.ToolsetKind => true,
                    _ => false
                };
            }

            return false;
        }

        private static bool IsAncestor(ToolboxTreeNode source, ToolboxTreeNode target)
        {
            var current = target.ParentNode;
            while (current is not null)
            {
                if (ReferenceEquals(current, source))
                    return true;

                current = current.ParentNode;
            }

            return false;
        }

        private static bool MoveNode(ToolboxTreeNode source, ToolboxTreeNode target, DropPlacement placement)
        {
            return source.Kind switch
            {
                ToolboxTreeNode.ToolKind => MoveToolNode(source, target, placement),
                ToolboxTreeNode.ToolsetKind => MoveToolsetNode(source, target, placement),
                _ => false
            };
        }

        private static bool MoveToolNode(ToolboxTreeNode source, ToolboxTreeNode target, DropPlacement placement)
        {
            if (source.Tool is null || source.ParentToolset is null || target.OwnerCatalog is null)
                return false;

            var tool = source.Tool;
            source.ParentToolset.Tools.Remove(tool);

            if (target.Kind == ToolboxTreeNode.CatalogKind)
            {
                EnsureDefaultRootToolset(target.OwnerCatalog).Tools.Add(tool);
                return true;
            }

            if (target.Kind == ToolboxTreeNode.ToolsetKind && target.Toolset is not null)
            {
                target.Toolset.Tools.Add(tool);
                return true;
            }

            if (target.Kind == ToolboxTreeNode.ToolKind && target.ParentToolset is not null && target.Tool is not null)
            {
                var destination = target.ParentToolset.Tools;
                var index = destination.IndexOf(target.Tool);
                if (index < 0)
                    index = destination.Count;

                if (placement == DropPlacement.After)
                    index++;

                destination.Insert(index, tool);
                return true;
            }

            return false;
        }

        private static bool MoveToolsetNode(ToolboxTreeNode source, ToolboxTreeNode target, DropPlacement placement)
        {
            if (source.Toolset is null || source.OwnerCatalog is null || target.OwnerCatalog is null)
                return false;

            var sourceCollection = source.ParentToolset is not null
                ? source.ParentToolset.Children
                : source.OwnerCatalog.Toolsets;

            sourceCollection.Remove(source.Toolset);

            if (target.Kind == ToolboxTreeNode.CatalogKind)
            {
                target.OwnerCatalog.Toolsets.Add(source.Toolset);
                return true;
            }

            if (target.Kind == ToolboxTreeNode.ToolsetKind && target.Toolset is not null)
            {
                if (placement == DropPlacement.Inside)
                {
                    target.Toolset.Children.Add(source.Toolset);
                    return true;
                }

                var destination = target.ParentToolset is not null
                    ? target.ParentToolset.Children
                    : target.OwnerCatalog.Toolsets;

                var index = destination.IndexOf(target.Toolset);
                if (index < 0)
                    index = destination.Count;

                if (placement == DropPlacement.After)
                    index++;

                destination.Insert(index, source.Toolset);
                return true;
            }

            return false;
        }

        private static string FormatDropStatus(ToolboxTreeNode source, ToolboxTreeNode target, DropPlacement placement)
        {
            var effectivePlacement = source.Kind == ToolboxTreeNode.ToolKind &&
                                     (target.Kind == ToolboxTreeNode.CatalogKind || target.Kind == ToolboxTreeNode.ToolsetKind)
                ? DropPlacement.Inside
                : source.Kind == ToolboxTreeNode.ToolsetKind && target.Kind == ToolboxTreeNode.CatalogKind
                    ? DropPlacement.Inside
                    : placement;

            var action = effectivePlacement switch
            {
                DropPlacement.Before => "移动到前面",
                DropPlacement.After => "移动到后面",
                _ => "移动到内部"
            };

            return $"{source.EditableName} -> {target.EditableName}（{action}）";
        }

        private ToolboxTreeNode? ResolveTreeNodeFromElement(DependencyObject? element)
        {
            var container = FindAncestor<TreeViewItem>(element);
            return container?.DataContext as ToolboxTreeNode;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T matched)
                    return matched;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static TreeViewItem? FindTreeViewItem(ItemsControl parent, object item)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem direct)
                return direct;

            foreach (var child in parent.Items)
            {
                if (parent.ItemContainerGenerator.ContainerFromItem(child) is not TreeViewItem childContainer)
                    continue;

                childContainer.IsExpanded = true;
                var result = FindTreeViewItem(childContainer, item);
                if (result is not null)
                    return result;
            }

            return null;
        }

        private sealed class LoadResult
        {
            public List<ToolboxCatalog> Catalogs { get; } = new();

            public List<string> Failures { get; } = new();

            public string MenuMode { get; set; } = string.Empty;
        }

        private sealed class ToolsetCollectionTarget
        {
            public ToolsetCollectionTarget(ToolboxCatalog catalog, ToolboxToolset? parentToolset, IList<ToolboxToolset> collection)
            {
                Catalog = catalog;
                ParentToolset = parentToolset;
                Collection = collection;
            }

            public ToolboxCatalog Catalog { get; }

            public ToolboxToolset? ParentToolset { get; }

            public IList<ToolboxToolset> Collection { get; }
        }

        private sealed class DropTargetInfo
        {
            public DropTargetInfo(ToolboxTreeNode target, DropPlacement placement)
            {
                Target = target;
                Placement = placement;
            }

            public ToolboxTreeNode Target { get; }

            public DropPlacement Placement { get; }
        }

        private enum DropPlacement
        {
            Before,
            Inside,
            After
        }

}
}
