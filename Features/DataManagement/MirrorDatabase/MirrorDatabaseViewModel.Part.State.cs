using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace XIAOFUTools.Features.DataManagement.MirrorDatabase
{
    public partial class MirrorDatabaseViewModel
    {

        public string SourceDatabasePath
        {
            get { return _sourceDatabasePath; }
            set
            {
                SetProperty(ref _sourceDatabasePath, value);
                if (!string.IsNullOrEmpty(value))
                {
                    var dirName = Path.GetFileName(value);
                    if (dirName.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                    {
                        DatabaseName = dirName.Substring(0, dirName.Length - 4) + "_Mirror";
                    }
                }
            }
        }

        public string OutputFolderPath
        {
            get { return _outputFolderPath; }
            set { SetProperty(ref _outputFolderPath, value); }
        }

        public string DatabaseName
        {
            get { return _databaseName; }
            set { SetProperty(ref _databaseName, value); }
        }

        public bool IsProcessing
        {
            get { return _isProcessing; }
            set { SetProperty(ref _isProcessing, value); }
        }

        public string LogText
        {
            get { return _logText; }
            set { SetProperty(ref _logText, value); }
        }

        public ICommand BrowseSourceDatabaseCommand { get; private set; }
        public ICommand BrowseOutputFolderCommand { get; private set; }
        public ICommand StartCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }
    }
}
