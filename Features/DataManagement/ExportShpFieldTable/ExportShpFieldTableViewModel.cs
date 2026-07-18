using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Features.DataManagement.ExportShpFieldTable.Infrastructure;

namespace XIAOFUTools.Features.DataManagement.ExportShpFieldTable
{
    /// <summary>
    /// SHP输字段表视图模型
    /// </summary>
    public partial class ExportShpFieldTableViewModel : PropertyChangedBase
    {
        private readonly ShpSchemaFileStore _fileStore = new();
        private string _inputFolderPath;
        private string _outputExcelPath;
        private bool _isProcessing;
        private string _logText;
        private CancellationTokenSource _cancellationTokenSource;

        public ExportShpFieldTableViewModel()
        {
            _inputFolderPath = string.Empty;
            _outputExcelPath = string.Empty;
            _logText = string.Empty;

            BrowseInputFolderCommand = new RelayCommand(() => BrowseInputFolder(), () => !IsProcessing);
            BrowseOutputExcelCommand = new RelayCommand(() => BrowseOutputExcel(), () => !IsProcessing);
            StartCommand = new RelayCommand(() => StartExport(), () => CanStart());
            StopCommand = new RelayCommand(() => StopExport(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
        }
    }
}
