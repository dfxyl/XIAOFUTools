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
        private void AddSystemTools(bool forceRoot)
        {
            var target = ResolveProjectInsertTarget(forceRoot);
            if (target is null)
            {
                MessageBox.Show(forceRoot ? "请选择一个自定义组。" : "请选择一个自定义组、工具集或工具节点。", "GIS 工具口袋");
                return;
            }

            var picker = new SystemToolPickerWindow
            {
                Owner = this
            };

            if (picker.ShowDialog() != true)
                return;

            ToolboxTool? lastAdded = null;
            foreach (var item in picker.SelectedTools)
            {
                if (ContainsTool(target, item.ToolPath))
                    continue;

                lastAdded = new ToolboxTool
                {
                    Name = item.Name,
                    Caption = item.Caption,
                    ToolPath = item.ToolPath,
                    SourceToolPath = item.ToolPath,
                    ToolKind = item.ToolKind
                };
                target.Tools.Add(lastAdded);
            }

            RefreshTreeAndReveal(lastAdded ?? (object)target);
            UpdateStatus();
        }

        private void AddOrReplace(ToolboxCatalog catalog)
        {
            ToolboxApplicationService.NormalizeCatalog(catalog);
            var existing = Toolboxes.FirstOrDefault(item => IsSameCatalog(item, catalog));
            if (existing is null)
            {
                Toolboxes.Add(catalog);
                return;
            }

            var index = Toolboxes.IndexOf(existing);
            if (!string.IsNullOrWhiteSpace(existing.DisplayName))
                catalog.DisplayName = existing.DisplayName;

            if (string.IsNullOrWhiteSpace(catalog.OriginalToolboxPath))
                catalog.OriginalToolboxPath = existing.OriginalToolboxPath;

            catalog.MenuMode = existing.MenuMode;
            Toolboxes[index] = catalog;
        }

        private static bool IsSameCatalog(ToolboxCatalog left, ToolboxCatalog right)
        {
            if (ToolboxApplicationService.IsCustomCatalog(left) || ToolboxApplicationService.IsCustomCatalog(right))
                return PathsEqual(left.ToolboxPath, right.ToolboxPath);

            return PathsEqual(left.ToolboxPath, right.ToolboxPath) ||
                   PathsEqual(left.OriginalToolboxPath, right.OriginalToolboxPath) ||
                   PathsEqual(left.ToolboxPath, right.OriginalToolboxPath) ||
                   PathsEqual(left.OriginalToolboxPath, right.ToolboxPath);
        }

        private static bool PathsEqual(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static ToolboxCatalog PrepareCatalogForStorage(ToolboxCatalog catalog, out bool migrated)
        {
            migrated = false;
            if (string.IsNullOrWhiteSpace(catalog.ToolboxPath) ||
                ToolboxApplicationService.IsCustomCatalog(catalog) ||
                ToolboxApplicationService.IsInternalPath(catalog.ToolboxPath) ||
                !File.Exists(catalog.ToolboxPath))
            {
                return catalog;
            }

            var internalPath = ToolboxApplicationService.Import(catalog.ToolboxPath);
            var imported = ToolboxApplicationService.LoadToolbox(internalPath);
            imported.DisplayName = catalog.DisplayName;
            imported.MenuMode = catalog.MenuMode;
            imported.OriginalToolboxPath = string.IsNullOrWhiteSpace(catalog.OriginalToolboxPath)
                ? catalog.ToolboxPath
                : catalog.OriginalToolboxPath;
            migrated = true;
            return imported;
        }

        private static LoadResult MigrateExternalCatalogs(IEnumerable<ToolboxCatalog> catalogs)
        {
            var result = new LoadResult();
            foreach (var catalog in catalogs)
            {
                try
                {
                    result.Catalogs.Add(PrepareCatalogForStorage(catalog, out _));
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{catalog.SourceFileName}：{ex.Message}");
                }
            }

            return result;
        }

        private async void ReloadCatalogs(IReadOnlyCollection<ToolboxCatalog> catalogs)
        {
            if (catalogs.Count == 0)
                return;

            var selectedPaths = SelectedCatalogs().Select(item => item.ToolboxPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            SetBusy(true, "正在刷新工具箱...");
            try
            {
                var result = await Task.Run(() => ReloadCatalogFiles(catalogs));
                foreach (var catalog in result.Catalogs)
                    AddOrReplace(catalog);

                ToolboxList.Items.Refresh();
                ToolboxList.SelectedItems.Clear();
                foreach (var catalog in Toolboxes.Where(item => selectedPaths.Contains(item.ToolboxPath)))
                    ToolboxList.SelectedItems.Add(catalog);

                if (ToolboxList.SelectedItem is null && Toolboxes.Count > 0)
                    ToolboxList.SelectedIndex = 0;

                RefreshTree();
                UpdateStatus();
                if (result.Failures.Count > 0)
                    MessageBox.Show(string.Join(Environment.NewLine, result.Failures), "工具箱刷新失败");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private List<ToolboxCatalog> SelectedCatalogs()
        {
            return ToolboxList.SelectedItems.Cast<ToolboxCatalog>().ToList();
        }

        private void RestoreSelection(IEnumerable<ToolboxCatalog> selected)
        {
            ToolboxList.SelectedItems.Clear();
            foreach (var catalog in selected)
                ToolboxList.SelectedItems.Add(catalog);

            if (ToolboxList.SelectedItem is null && Toolboxes.Count > 0)
                ToolboxList.SelectedIndex = 0;

            RefreshTree();
        }

        private void SelectCatalogs(IEnumerable<ToolboxCatalog> catalogs)
        {
            var paths = catalogs
                .Select(catalog => catalog.ToolboxPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            ToolboxList.SelectedItems.Clear();
            foreach (var catalog in Toolboxes.Where(item => paths.Contains(item.ToolboxPath)))
                ToolboxList.SelectedItems.Add(catalog);

            if (ToolboxList.SelectedItem is null && Toolboxes.Count > 0)
                ToolboxList.SelectedIndex = 0;

            RefreshTree();
        }

        private void RefreshTree()
        {
            TreeNodes.Clear();

            var selected = ToolboxList.SelectedItems.Cast<ToolboxCatalog>().ToList();
            if (selected.Count == 0 && ToolboxList.SelectedItem is ToolboxCatalog single)
                selected.Add(single);

            if (selected.Count == 0)
            {
                TreeTitleText.Text = "结构树";
                TreeHintText.Text = "未选择来源";
                ClearSelectedNodeDetails();
                return;
            }

            TreeTitleText.Text = selected.Count == 1 ? selected[0].DisplayTitle : $"已选 {selected.Count} 个来源";
            TreeHintText.Text = $"{selected.Sum(catalog => catalog.ToolsetCount)} 个工具集 / {selected.Sum(catalog => catalog.ToolCount)} 个工具";

            foreach (var catalog in selected)
                TreeNodes.Add(ToolboxTreeNode.FromCatalog(catalog));
        }

        private void RefreshTreeAndReveal(object? model)
        {
            RefreshTree();
            if (model is null)
                return;

            var node = FindNodeByModel(TreeNodes, model);
            if (node is null)
                return;

            ExpandAncestors(node);
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                var container = FindTreeViewItem(ToolTree, node);
                if (container is null)
                    return;

                container.IsSelected = true;
                container.BringIntoView();
            }));
        }

        private static void ExpandAncestors(ToolboxTreeNode node)
        {
            var current = node.ParentNode;
            while (current is not null)
            {
                current.IsExpanded = true;
                current = current.ParentNode;
            }
        }

        private static ToolboxTreeNode? FindNodeByModel(IEnumerable<ToolboxTreeNode> nodes, object model)
        {
            foreach (var node in nodes)
            {
                if (ReferenceEquals(node.OwnerCatalog, model) ||
                    ReferenceEquals(node.Toolset, model) ||
                    ReferenceEquals(node.Tool, model))
                {
                    return node;
                }

                var child = FindNodeByModel(node.Children, model);
                if (child is not null)
                    return child;
            }

            return null;
        }

        private static void SetNodeExpanded(ToolboxTreeNode node, bool isExpanded)
        {
            node.IsExpanded = isExpanded;
            foreach (var child in node.Children)
                SetNodeExpanded(child, isExpanded);
        }

        private void ClearSelectedNodeDetails()
        {
            SelectedNodeTypeText.Text = "-";
            SelectedNodeNameText.Text = "-";
            SelectedNodePathText.Text = "-";
        }

        private void UpdateStatus()
        {
            var toolsetCount = Toolboxes.Sum(catalog => catalog.ToolsetCount);
            var toolCount = Toolboxes.Sum(catalog => catalog.ToolCount);
            StatusText.Text = $"{Toolboxes.Count} 个来源，{toolsetCount} 个工具集，{toolCount} 个工具";
        }

        private void SetBusy(bool isBusy, string? message = null)
        {
            _isBusy = isBusy;
            IsEnabled = !isBusy;
            if (!string.IsNullOrWhiteSpace(message))
                StatusText.Text = message;
            else
                UpdateStatus();
        }

        private string? PromptForText(string title, string label, string initialValue)
        {
            var input = new TextBox
            {
                Text = initialValue,
                MinWidth = 300,
                Margin = new Thickness(0, 6, 0, 0)
            };

            var okButton = new Button
            {
                Content = "确定",
                Width = 72,
                Height = 26,
                Margin = new Thickness(6, 0, 0, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "取消",
                Width = 72,
                Height = 26,
                Margin = new Thickness(6, 0, 0, 0),
                IsCancel = true
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);

            var content = new StackPanel
            {
                Margin = new Thickness(12)
            };
            content.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 39, 46))
            });
            content.Children.Add(input);
            content.Children.Add(buttons);

            var dialog = new Window
            {
                Title = title,
                Owner = this,
                Content = content,
                Width = 360,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };

            okButton.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(input.Text))
                    return;

                dialog.DialogResult = true;
            };

            input.TextChanged += (_, _) => okButton.IsEnabled = !string.IsNullOrWhiteSpace(input.Text);
            input.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                input.Focus();
                input.SelectAll();
            }));

            return dialog.ShowDialog() == true ? input.Text.Trim() : null;
        }

        private static LoadResult LoadToolboxFiles(IEnumerable<string> fileNames)
        {
            var result = new LoadResult();
            foreach (var fileName in fileNames)
            {
                try
                {
                    var internalPath = ToolboxApplicationService.Import(fileName);
                    var catalog = ToolboxApplicationService.LoadToolbox(internalPath);
                    catalog.OriginalToolboxPath = fileName;
                    result.Catalogs.Add(catalog);
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{Path.GetFileName(fileName)}：{ex.Message}");
                }
            }

            return result;
        }

        private static LoadResult ImportPackageFiles(IEnumerable<string> fileNames)
        {
            var result = new LoadResult();
            foreach (var fileName in fileNames)
            {
                try
                {
                    var importResult = ToolboxApplicationService.ImportPackage(fileName);
                    if (!string.IsNullOrWhiteSpace(importResult.MenuMode))
                        result.MenuMode = importResult.MenuMode;

                    result.Catalogs.AddRange(importResult.Catalogs);
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{Path.GetFileName(fileName)}：{ex.Message}");
                }
            }

            return result;
        }

        private static LoadResult ReloadCatalogFiles(IEnumerable<ToolboxCatalog> catalogs)
        {
            var result = new LoadResult();
            foreach (var catalog in catalogs.ToList())
            {
                try
                {
                    if (ToolboxApplicationService.IsCustomCatalog(catalog))
                    {
                        result.Catalogs.Add(catalog);
                        continue;
                    }

                    var reloaded = ToolboxApplicationService.LoadToolbox(catalog.ToolboxPath);
                    reloaded.DisplayName = catalog.DisplayName;
                    reloaded.MenuMode = catalog.MenuMode;
                    reloaded.OriginalToolboxPath = catalog.OriginalToolboxPath;
                    reloaded.CatalogKind = catalog.CatalogKind;
                    result.Catalogs.Add(reloaded);
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{catalog.SourceFileName}：{ex.Message}");
                }
            }

            return result;
        }

        private string? ResolveInitialDirectory()
        {
            var selected = SelectedCatalogs().FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(selected?.ToolboxPath) && !ToolboxApplicationService.IsCustomCatalog(selected))
            {
                var directory = Path.GetDirectoryName(selected.ToolboxPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    return directory;
            }

            var first = Toolboxes.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first?.ToolboxPath) && !ToolboxApplicationService.IsCustomCatalog(first))
            {
                var directory = Path.GetDirectoryName(first.ToolboxPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    return directory;
            }

            var projectHome = Project.Current?.HomeFolderPath;
            if (!string.IsNullOrWhiteSpace(projectHome) && Directory.Exists(projectHome))
                return projectHome;

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Directory.Exists(documents) ? documents : null;
        }

        private void SelectMenuMode(string menuMode)
        {
            foreach (var item in MenuModeCombo.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is string tag && tag.Equals(menuMode, StringComparison.OrdinalIgnoreCase))
                {
                    MenuModeCombo.SelectedItem = item;
                    return;
                }
            }

            MenuModeCombo.SelectedIndex = 0;
        }

        private ToolboxToolset? ResolveProjectInsertTarget(bool forceRoot)
        {
            if (ToolTree.SelectedItem is ToolboxTreeNode node &&
                node.OwnerCatalog is not null &&
                ToolboxApplicationService.IsCustomCatalog(node.OwnerCatalog))
            {
                if (forceRoot)
                    return EnsureDefaultRootToolset(node.OwnerCatalog);

                if (node.Toolset is not null)
                    return node.Toolset;

                if (node.Tool is not null && node.ParentToolset is not null)
                    return node.ParentToolset;

                if (node.Kind == ToolboxTreeNode.CatalogKind)
                    return EnsureDefaultRootToolset(node.OwnerCatalog);
            }

            var selectedCatalog = ToolboxList.SelectedItem as ToolboxCatalog;
            if (selectedCatalog is not null && ToolboxApplicationService.IsCustomCatalog(selectedCatalog))
                return EnsureDefaultRootToolset(selectedCatalog);

            return null;
        }

        private ToolsetCollectionTarget? ResolveToolsetCollectionTarget()
        {
            if (ToolTree.SelectedItem is ToolboxTreeNode node &&
                node.OwnerCatalog is not null &&
                ToolboxApplicationService.IsCustomCatalog(node.OwnerCatalog))
            {
                if (node.Kind == ToolboxTreeNode.CatalogKind)
                    return new ToolsetCollectionTarget(node.OwnerCatalog, null, node.OwnerCatalog.Toolsets);

                if (node.Toolset is not null)
                    return new ToolsetCollectionTarget(node.OwnerCatalog, node.Toolset, node.Toolset.Children);

                if (node.Tool is not null)
                {
                    if (node.ParentNode?.Kind == ToolboxTreeNode.CatalogKind)
                        return new ToolsetCollectionTarget(node.OwnerCatalog, null, node.OwnerCatalog.Toolsets);

                    if (node.ParentNode?.Toolset is not null)
                        return new ToolsetCollectionTarget(node.OwnerCatalog, node.ParentNode.Toolset, node.ParentNode.Toolset.Children);
                }
            }

            var selectedCatalog = ToolboxList.SelectedItem as ToolboxCatalog;
            if (selectedCatalog is not null && ToolboxApplicationService.IsCustomCatalog(selectedCatalog))
                return new ToolsetCollectionTarget(selectedCatalog, null, selectedCatalog.Toolsets);

            return null;
        }

        private static ToolboxToolset EnsureDefaultRootToolset(ToolboxCatalog catalog)
        {
            var toolset = catalog.Toolsets.FirstOrDefault(item => ToolboxApplicationService.IsDefaultToolsetName(item.Name));
            if (toolset is not null)
                return toolset;

            toolset = new ToolboxToolset { Name = string.Empty };
            catalog.Toolsets.Insert(0, toolset);
            return toolset;
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new System.Text.StringBuilder(value.Length);
            foreach (var character in value)
                builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);

            return builder.Length == 0 ? "toolbox" : builder.ToString();
        }

        private string CreateUniqueCatalogName()
        {
            const string baseName = "自定义组";
            var existing = Toolboxes.Select(item => item.DisplayTitle).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            if (!existing.Contains(baseName))
                return baseName;

            for (var index = 2; index < 1000; index++)
            {
                var candidate = $"{baseName}{index}";
                if (!existing.Contains(candidate))
                    return candidate;
            }

            return $"{baseName}_{Guid.NewGuid().ToString("N")[..6]}";
        }

        private static string CreateUniqueToolsetName(IEnumerable<ToolboxToolset> siblings, string baseName)
        {
            var existing = siblings.Select(item => item.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            if (!existing.Contains(baseName))
                return baseName;

            for (var index = 2; index < 1000; index++)
            {
                var candidate = $"{baseName}{index}";
                if (!existing.Contains(candidate))
                    return candidate;
            }

            return $"{baseName}_{Guid.NewGuid().ToString("N")[..6]}";
        }

        private static bool ContainsTool(ToolboxToolset toolset, string toolPath)
        {
            return toolset.Tools.Any(tool => tool.ToolPath.Equals(toolPath, StringComparison.OrdinalIgnoreCase));
        }

        private static ToolboxTreeNode? GetDraggedNode(IDataObject data)
        {
            return data.GetDataPresent(typeof(ToolboxTreeNode))
                ? data.GetData(typeof(ToolboxTreeNode)) as ToolboxTreeNode
                : null;
        }

        private DropTargetInfo? ResolveDropTarget(DragEventArgs e)
        {
            var targetNode = ResolveTreeNodeFromElement(e.OriginalSource as DependencyObject);
            var container = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (targetNode is null || container is null)
                return null;

            var position = e.GetPosition(container);
            var placement = DetermineDropPlacement(targetNode, container, position);
            return new DropTargetInfo(targetNode, placement);
        }

    }
}
