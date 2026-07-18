using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Features.Analysis.MultiOverlaySummary
{
    public partial class MultiOverlaySummaryResultWindow
    {

        private void CopySelectedCells_Click(object sender, RoutedEventArgs e)
        {
            if (ResultDataGrid.SelectedCells.Count > 0)
                ApplicationCommands.Copy.Execute(null, ResultDataGrid);
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e) => ResultDataGrid.SelectAll();
    }
}
