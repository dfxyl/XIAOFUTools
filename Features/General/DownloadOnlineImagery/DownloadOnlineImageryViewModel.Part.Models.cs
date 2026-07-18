using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks; 
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Text;
using System.Globalization;

namespace XIAOFUTools.Features.General.DownloadOnlineImagery
{
    internal partial class DownloadOnlineImageryViewModel
    {

        /// <summary>
        /// 格网参数
        /// </summary>
        private class GridParameters
        {
            public double CellWidth { get; set; }
            public double CellHeight { get; set; }
            public int GridCount { get; set; }
        }
    }
}
