using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ExcelDataReader;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.ShapefileBuilder
{
    public partial class ShapefileBuilderViewModel
    {

        /// <summary>
        /// 判断是否可开始
        /// </summary>
        private bool CanStart()
        {
            return !IsProcessing
                   && !string.IsNullOrWhiteSpace(InputExcelPath)
                   && !string.IsNullOrWhiteSpace(OutputFolderPath)
                   && SelectedSpatialReference != null;
        }
    }
}
