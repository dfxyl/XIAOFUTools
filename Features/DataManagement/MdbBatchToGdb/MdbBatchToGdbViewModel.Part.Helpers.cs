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
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.MdbBatchToGdb
{
    internal partial class MdbBatchToGdbViewModel
    {

        private void ReplaceItems(IEnumerable<string> mdbFiles)
        {
            foreach (MdbFileItem item in MdbItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            MdbItems.Clear();
            foreach (string mdbPath in mdbFiles)
            {
                var item = new MdbFileItem
                {
                    IsSelected = true,
                    Name = Path.GetFileNameWithoutExtension(mdbPath),
                    FullPath = mdbPath,
                    RelativePath = _fileStore.BuildRelativePath(InputFolderPath, mdbPath)
                };

                item.PropertyChanged += OnItemPropertyChanged;
                MdbItems.Add(item);
            }

            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void ClearItems()
        {
            foreach (MdbFileItem item in MdbItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }

            MdbItems.Clear();
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
