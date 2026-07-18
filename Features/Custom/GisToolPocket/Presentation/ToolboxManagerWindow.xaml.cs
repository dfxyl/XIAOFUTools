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
        public ObservableCollection<ToolboxCatalog> Toolboxes { get; } = new();

        public ObservableCollection<ToolboxTreeNode> TreeNodes { get; } = new();

        private string _menuMode = "Auto";
        private bool _isBusy;
        private Point _dragStartPoint;
        private ToolboxTreeNode? _dragSourceNode;

        public ToolboxManagerWindow()
        {
            InitializeComponent();
            DataContext = this;

            var configuration = ToolboxApplicationService.Load();
            _menuMode = configuration.MenuMode;
            SelectMenuMode(_menuMode);

            foreach (var catalog in configuration.Catalogs)
                Toolboxes.Add(catalog);

            if (Toolboxes.Count > 0)
                ToolboxList.SelectedIndex = 0;

            UpdateStatus();
            Loaded += ToolboxManagerWindow_Loaded;
        }

        private async void ToolboxManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var externalCatalogs = Toolboxes
                .Where(catalog => !string.IsNullOrWhiteSpace(catalog.ToolboxPath) &&
                                  !ToolboxApplicationService.IsCustomCatalog(catalog) &&
                                  !ToolboxApplicationService.IsInternalPath(catalog.ToolboxPath) &&
                                  File.Exists(catalog.ToolboxPath))
                .ToList();

            if (externalCatalogs.Count == 0)
                return;

            SetBusy(true, "正在迁移外部工具箱...");
            try
            {
                var result = await Task.Run(() => MigrateExternalCatalogs(externalCatalogs));
                foreach (var catalog in result.Catalogs)
                    AddOrReplace(catalog);

                if (result.Catalogs.Count > 0)
                    ToolboxApplicationService.Save(Toolboxes, _menuMode);

                if (result.Failures.Count > 0)
                    MessageBox.Show(string.Join(Environment.NewLine, result.Failures), "工具箱迁移失败");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "添加工具箱",
                Filter = "ArcGIS 工具箱 (*.atbx;*.tbx;*.pyt)|*.atbx;*.tbx;*.pyt|ArcGIS 工具箱 (*.atbx)|*.atbx|传统工具箱 (*.tbx)|*.tbx|Python 工具箱 (*.pyt)|*.pyt",
                Multiselect = true,
                CheckFileExists = true
            };

            var initialDirectory = ResolveInitialDirectory();
            if (!string.IsNullOrWhiteSpace(initialDirectory))
                dialog.InitialDirectory = initialDirectory;

            if (dialog.ShowDialog(this) != true)
                return;

            SetBusy(true, "正在加载工具箱...");
            try
            {
                var result = await Task.Run(() => LoadToolboxFiles(dialog.FileNames));
                foreach (var catalog in result.Catalogs)
                    AddOrReplace(catalog);

                ToolboxList.Items.Refresh();
                SelectCatalogs(result.Catalogs);
                UpdateStatus();
                if (result.Failures.Count > 0)
                    MessageBox.Show(string.Join(Environment.NewLine, result.Failures), "工具箱加载失败");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void AddCustomGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var catalog = ToolboxApplicationService.CreateCatalog(CreateUniqueCatalogName());
            Toolboxes.Add(catalog);
            ToolboxList.Items.Refresh();
            ToolboxList.SelectedItem = catalog;
            RefreshTreeAndReveal(catalog);
            UpdateStatus();
        }

        private async void ImportPackageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "导入加载项完整工具包",
                Filter = "加载项完整工具包 (*.zip)|*.zip",
                Multiselect = true,
                CheckFileExists = true
            };

            var initialDirectory = ResolveInitialDirectory();
            if (!string.IsNullOrWhiteSpace(initialDirectory))
                dialog.InitialDirectory = initialDirectory;

            if (dialog.ShowDialog(this) != true)
                return;

            SetBusy(true, "正在导入加载项完整工具包...");
            try
            {
                var result = await Task.Run(() => ImportPackageFiles(dialog.FileNames));
                var wasEmpty = Toolboxes.Count == 0;
                if (wasEmpty && !string.IsNullOrWhiteSpace(result.MenuMode))
                {
                    _menuMode = result.MenuMode;
                    SelectMenuMode(_menuMode);
                }

                foreach (var catalog in result.Catalogs)
                    AddOrReplace(catalog);

                ToolboxList.Items.Refresh();
                SelectCatalogs(result.Catalogs);
                ToolboxApplicationService.Save(Toolboxes, _menuMode);
                ToolboxMenuSlotService.RefreshRibbon();
                UpdateStatus();
                if (result.Failures.Count > 0)
                    MessageBox.Show(string.Join(Environment.NewLine, result.Failures), "加载项完整工具包导入失败");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ExportPackageButton_Click(object sender, RoutedEventArgs e)
        {
            var catalogs = Toolboxes.ToList();
            if (catalogs.Count == 0)
            {
                MessageBox.Show("没有可导出的工具箱。", "GIS 工具口袋");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出加载项完整工具包",
                Filter = "加载项完整工具包 (*.zip)|*.zip",
                FileName = $"{SanitizeFileName("GIS_工具口袋_完整工具包")}.zip",
                AddExtension = true,
                OverwritePrompt = true
            };

            var initialDirectory = ResolveInitialDirectory();
            if (!string.IsNullOrWhiteSpace(initialDirectory))
                dialog.InitialDirectory = initialDirectory;

            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                ToolboxApplicationService.Export(catalogs, _menuMode, "GIS 工具口袋完整工具包", dialog.FileName);
                ToolboxApplicationService.SavePackage(catalogs, _menuMode);
                MessageBox.Show($"已导出加载项完整工具包：{dialog.FileName}", "GIS 工具口袋");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "加载项完整工具包导出失败");
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var catalog in SelectedCatalogs())
                Toolboxes.Remove(catalog);

            if (ToolboxList.SelectedItem is null && Toolboxes.Count > 0)
                ToolboxList.SelectedIndex = 0;

            RefreshTree();
            UpdateStatus();
        }

        private void ReloadSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = SelectedCatalogs();
            if (selected.Count == 0)
                selected = Toolboxes.ToList();

            ReloadCatalogs(selected);
        }

        private void ReloadAllButton_Click(object sender, RoutedEventArgs e)
        {
            ReloadCatalogs(Toolboxes.ToList());
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = SelectedCatalogs();
            foreach (var catalog in selected)
            {
                var index = Toolboxes.IndexOf(catalog);
                if (index > 0)
                    Toolboxes.Move(index, index - 1);
            }

            RestoreSelection(selected);
            UpdateStatus();
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = SelectedCatalogs();
            for (var i = selected.Count - 1; i >= 0; i--)
            {
                var catalog = selected[i];
                var index = Toolboxes.IndexOf(catalog);
                if (index >= 0 && index < Toolboxes.Count - 1)
                    Toolboxes.Move(index, index + 1);
            }

            RestoreSelection(selected);
            UpdateStatus();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ToolboxList.SelectedItems.Clear();
            Toolboxes.Clear();
            RefreshTree();
            ToolboxApplicationService.Save(Toolboxes, _menuMode);
            ToolboxApplicationService.ClearStorage();
            ToolboxMenuSlotService.RefreshRibbon();
            UpdateStatus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ToolboxApplicationService.Save(Toolboxes, _menuMode);
            ToolboxMenuSlotService.RefreshRibbon();
            DialogResult = true;
            Close();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ToolboxHelpWindow
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private void MenuModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string menuMode)
                _menuMode = menuMode;
        }

    }
}
