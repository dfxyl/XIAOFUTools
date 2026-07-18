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
        /// 判断是否可以开始
        /// </summary>
        private bool CanStart()
        {
            return !IsProcessing &&
                   !string.IsNullOrWhiteSpace(InputExcelPath) &&
                   !string.IsNullOrWhiteSpace(OutputFolderPath) &&
                   !string.IsNullOrWhiteSpace(DatabaseName) &&
                   SelectedSpatialReference != null;
        }
    }
}
