using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Tools.Settings;

namespace XIAOFUTools.Tools.BrowseFeatures
{
    public partial class BrowseFeaturesSettingsWindow : ProWindow
    {
        private readonly FeatureLayer _selectedLayer;
        private readonly List<string> _noteFieldNames = new List<string>();

        public BrowseFeaturesSettingsWindow(FeatureLayer selectedLayer)
        {
            InitializeComponent();
            _selectedLayer = selectedLayer;
            LoadSettings();
            LoadNoteFields();
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings.BrowseFeatures;
            ReviewerNameTextBox.Text = settings.ReviewerName ?? string.Empty;
            OutputGdbPathTextBox.Text = string.IsNullOrWhiteSpace(settings.OutputGdbPath)
                ? Project.Current?.DefaultGeodatabasePath ?? string.Empty
                : settings.OutputGdbPath;
            OutputTableNameTextBox.Text = string.IsNullOrWhiteSpace(settings.OutputTableName)
                ? $"BrowseFeaturesReview_{DateTime.Now:yyyyMMdd_HHmmss}"
                : settings.OutputTableName;
            BatchIdTextBox.Text = string.IsNullOrWhiteSpace(settings.BatchId)
                ? DateTime.Now.ToString("yyyyMMdd_HHmmss")
                : settings.BatchId;
            WriteNotesToFieldCheckBox.IsChecked = settings.WriteNotesToFeatureField;
        }

        private async void LoadNoteFields()
        {
            NotesFieldComboBox.ItemsSource = Array.Empty<string>();
            NotesFieldComboBox.IsEnabled = false;

            if (_selectedLayer == null)
            {
                return;
            }

            var fields = await QueuedTask.Run(() =>
            {
                using var table = _selectedLayer.GetTable();
                var definition = table?.GetDefinition();
                if (definition == null)
                {
                    return new List<string>();
                }

                return definition.GetFields()
                    .Where(field => field.FieldType == FieldType.String)
                    .Where(field => !string.Equals(field.Name, "OBJECTID", StringComparison.OrdinalIgnoreCase))
                    .Where(field => !string.Equals(field.Name, "OID", StringComparison.OrdinalIgnoreCase))
                    .Where(field => !string.Equals(field.Name, "FID", StringComparison.OrdinalIgnoreCase))
                    .Where(field => !string.Equals(field.Name, "GLOBALID", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(field => field.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(field => field.Name)
                    .ToList();
            });

            _noteFieldNames.Clear();
            _noteFieldNames.Add(string.Empty);
            _noteFieldNames.AddRange(fields);
            NotesFieldComboBox.ItemsSource = _noteFieldNames;

            var configuredField = SettingsManager.Settings.BrowseFeatures.NotesFieldName ?? string.Empty;
            NotesFieldComboBox.SelectedItem = _noteFieldNames.Contains(configuredField) ? configuredField : string.Empty;
            NotesFieldComboBox.IsEnabled = WriteNotesToFieldCheckBox.IsChecked == true && _noteFieldNames.Count > 1;
        }

        private void BrowseOutputGdbButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dialog = new OpenItemDialog
            {
                Title = "选择输出地理数据库",
                MultiSelect = false,
                Filter = ItemFilters.Geodatabases,
                InitialLocation = string.IsNullOrWhiteSpace(OutputGdbPathTextBox.Text)
                    ? Project.Current?.HomeFolderPath
                    : OutputGdbPathTextBox.Text
            };

            if (dialog.ShowDialog() == true && dialog.Items.Any())
            {
                OutputGdbPathTextBox.Text = dialog.Items.First().Path;
            }
        }

        private void WriteNotesToFieldCheckBox_Changed(object sender, System.Windows.RoutedEventArgs e)
        {
            NotesFieldComboBox.IsEnabled = WriteNotesToFieldCheckBox.IsChecked == true && _noteFieldNames.Count > 1;
        }

        private void SaveButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var settings = SettingsManager.Settings.BrowseFeatures;
            settings.ReviewerName = ReviewerNameTextBox.Text?.Trim() ?? string.Empty;
            settings.OutputGdbPath = OutputGdbPathTextBox.Text?.Trim() ?? string.Empty;
            settings.OutputTableName = OutputTableNameTextBox.Text?.Trim() ?? string.Empty;
            settings.BatchId = BatchIdTextBox.Text?.Trim() ?? string.Empty;
            settings.WriteNotesToFeatureField = WriteNotesToFieldCheckBox.IsChecked == true;
            settings.NotesFieldName = settings.WriteNotesToFeatureField
                ? NotesFieldComboBox.SelectedItem as string ?? string.Empty
                : string.Empty;
            SettingsManager.SaveSettings();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
