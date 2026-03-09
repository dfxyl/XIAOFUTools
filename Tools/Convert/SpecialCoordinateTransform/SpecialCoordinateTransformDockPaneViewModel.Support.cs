using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;

namespace XIAOFUTools.Tools.SpecialCoordinateTransform
{
    internal sealed partial class SpecialCoordinateTransformDockPaneViewModel
    {
        private void UpdateSingleOutputPath()
        {
            if (SelectedInputLayer == null || SelectedConversionType == null)
            {
                return;
            }

            string defaultGdb = ArcGIS.Desktop.Core.Project.Current?.DefaultGeodatabasePath;
            if (string.IsNullOrWhiteSpace(defaultGdb))
            {
                return;
            }

            string outputName = $"{SelectedInputLayer.Name}_{GetConversionShortName(SelectedConversionType.Key)}";
            OutputPath = Path.Combine(defaultGdb, outputName);
        }

        private void AddLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
            ExecuteOnUiThread(() => LogContent += line);
        }

        private void SetStatus(string message)
        {
            ExecuteOnUiThread(() => StatusMessage = message);
        }

        private void SetProgressIndeterminate(bool isIndeterminate)
        {
            ExecuteOnUiThread(() => IsProgressIndeterminate = isIndeterminate);
        }

        private void UpdateProgress(double value)
        {
            ExecuteOnUiThread(() => Progress = Math.Max(0, Math.Min(100, value)));
        }

        private void ResetItemStatuses(IEnumerable<BatchTransformItem> items)
        {
            foreach (BatchTransformItem item in items)
            {
                UpdateItemStatus(item, string.Empty);
            }
        }

        private void UpdateItemStatus(BatchTransformItem item, string status)
        {
            if (item == null)
            {
                return;
            }

            ExecuteOnUiThread(() => item.Status = status);
        }

        private static string GetConversionShortName(string conversionType)
        {
            return conversionType switch
            {
                "wgs84_to_gcj02" => "WGS84ToGCJ02",
                "gcj02_to_wgs84" => "GCJ02ToWGS84",
                "gcj02_to_bd09" => "GCJ02ToBD09",
                "bd09_to_gcj02" => "BD09ToGCJ02",
                "wgs84_to_bd09" => "WGS84ToBD09",
                "bd09_to_wgs84" => "BD09ToWGS84",
                _ => "Converted"
            };
        }

        private static string GetMirroredOutputFolder(string inputRootFolder, string sourceFolder, string outputRootFolder)
        {
            if (string.IsNullOrWhiteSpace(outputRootFolder))
            {
                return sourceFolder;
            }

            try
            {
                string relative = Path.GetRelativePath(inputRootFolder, sourceFolder);
                return string.Equals(relative, ".", StringComparison.Ordinal)
                    ? outputRootFolder
                    : Path.Combine(outputRootFolder, relative);
            }
            catch
            {
                return outputRootFolder;
            }
        }

        private sealed class DatasetTransformPlan
        {
            public BatchTransformItem OwnerItem { get; init; }

            public string DisplayName { get; init; } = string.Empty;

            public string OutputPath { get; init; } = string.Empty;

            public string DefaultOutputName { get; init; } = string.Empty;

            public object TemplateValue { get; init; }

            public InputSourceKind SourceKind { get; init; }

            public ArcGIS.Desktop.Mapping.FeatureLayer FeatureLayer { get; init; }

            public string ShapefilePath { get; init; }

            public string InputGdbPath { get; init; }

            public string RelativePathInGdb { get; init; }

            public string FeatureDatasetName { get; init; }
        }

        private sealed class GdbTransformPlan
        {
            public BatchTransformItem OwnerItem { get; init; }

            public string OutputGdbPath { get; init; } = string.Empty;

            public List<DatasetTransformPlan> DatasetPlans { get; init; } = new();
        }

        private sealed class DatasetHandle : IDisposable
        {
            private readonly List<IDisposable> _disposables;

            public DatasetHandle(FeatureClass featureClass, params IDisposable[] disposables)
            {
                FeatureClass = featureClass;
                _disposables = disposables
                    .Where(disposable => disposable != null)
                    .Distinct()
                    .ToList();
            }

            public FeatureClass FeatureClass { get; }

            public void Dispose()
            {
                foreach (IDisposable disposable in _disposables)
                {
                    disposable.Dispose();
                }
            }
        }

        private enum InputSourceKind
        {
            MapLayer,
            Shapefile,
            FileGeodatabase
        }
    }

    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
