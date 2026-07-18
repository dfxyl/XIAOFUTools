using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.BatchMergeShp
{
    internal partial class BatchMergeShpViewModel
    {

        private void ReplaceItems(IEnumerable<string> shpFiles)
        {
            foreach (var item in ShpItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            ShpItems.Clear();
            foreach (var shpPath in shpFiles)
            {
                var item = new ShpMergeItem
                {
                    IsSelected = true,
                    Name = Path.GetFileNameWithoutExtension(shpPath),
                    Directory = Path.GetDirectoryName(shpPath) ?? string.Empty,
                    FullPath = shpPath
                };
                item.GeometryKind = GetGeometryKind(shpPath);
                item.GeometryType = GetGeometryDisplayName(item.GeometryKind);

                item.PropertyChanged += OnItemPropertyChanged;
                ShpItems.Add(item);
            }

            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void ClearItems()
        {
            foreach (var item in ShpItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            ShpItems.Clear();
            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void SetScanning(bool scanning)
        {
            if (_isScanning == scanning)
            {
                return;
            }

            _isScanning = scanning;
            NotifyPropertyChanged(() => IsBusy);
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void AppendInfo(string message)
        {
            AppendLog(message);
        }

        private void AppendWarning(string message)
        {
            AppendLog("警告: " + message);
        }

        private void AppendError(string message)
        {
            AppendLog("错误: " + message);
        }
    }
}
