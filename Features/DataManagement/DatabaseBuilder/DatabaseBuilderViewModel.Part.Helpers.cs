using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;
using ExcelDataReader;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.DatabaseBuilder
{
    public partial class DatabaseBuilderViewModel
    {

        /// <summary>
        /// 初始化基本属性
        /// </summary>
        private void Initialize()
        {
            _inputExcelPath = "";
            _outputFolderPath = "";
            _databaseName = "NewDatabase";
            _isProcessing = false;
            _logText = "";
            _selectedSpatialReference = SpatialReferences.WGS84;
            _selectedCoordinateSystemName = $"{_selectedSpatialReference.Name} (WKID: {_selectedSpatialReference.Wkid})";
        }

        /// <summary>
        /// 初始化命令
        /// </summary>
        private void InitializeCommands()
        {
            BrowseInputExcelCommand = new RelayCommand(() => BrowseInputExcel(), () => !IsProcessing);
            BrowseOutputFolderCommand = new RelayCommand(() => BrowseOutputFolder(), () => !IsProcessing);
            ExportTemplateCommand = new RelayCommand(() => ExportTemplate(), () => !IsProcessing);
            SelectCoordinateSystemCommand = new RelayCommand(() => SelectCoordinateSystem(), () => !IsProcessing);
            StartCommand = new RelayCommand(() => StartBuildDatabase(), () => CanStart());
            StopCommand = new RelayCommand(() => StopBuildDatabase(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
        }
    }
}
