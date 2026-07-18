using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;

namespace XIAOFUTools.Features.DataManagement.ExportDatabaseSchema
{
    public partial class ExportDatabaseSchemaViewModel
    {

        private void Initialize()
        {
            _inputGdbPath = "";
            _outputExcelPath = "";
            _isProcessing = false;
            _logText = "";
        }

        private void InitializeCommands()
        {
            BrowseInputGdbCommand = new RelayCommand(() => BrowseInputGdb(), () => !IsProcessing);
            BrowseOutputExcelCommand = new RelayCommand(() => BrowseOutputExcel(), () => !IsProcessing);
            StartCommand = new RelayCommand(() => StartExport(), () => CanStart());
            StopCommand = new RelayCommand(() => StopExport(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
        }

        private bool IsSystemField(string fieldName)
        {
            var systemFields = new[] { "OBJECTID", "Shape", "Shape_Length", "Shape_Area", "GlobalID" };
            return systemFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
        }

        private string SanitizeSheetName(string name)
        {
            // Excel工作表名称不能包含这些字符: \ / ? * [ ] :
            var invalidChars = new[] { '\\', '/', '?', '*', '[', ']', ':' };
            foreach (var c in invalidChars)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
