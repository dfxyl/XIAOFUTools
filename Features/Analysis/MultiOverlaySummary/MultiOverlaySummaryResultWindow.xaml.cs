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
    public partial class MultiOverlaySummaryResultWindow : Window
    {
        private DataTable _dataTable;
        private int _decimalPlaces;
        private List<IntersectGeometryItem> _intersectGeometries;
        private SpatialReference _spatialReference;
        private string _areaUnit;

        public MultiOverlaySummaryResultWindow(DataTable dataTable, int decimalPlaces, 
            List<IntersectGeometryItem> intersectGeometries = null, SpatialReference spatialReference = null,
            string areaUnit = "平方米")
        {
            InitializeComponent();
            _dataTable = dataTable;
            _decimalPlaces = decimalPlaces;
            _intersectGeometries = intersectGeometries;
            _spatialReference = spatialReference;
            _areaUnit = areaUnit;

            ResultDataGrid.ItemsSource = dataTable.DefaultView;
            InfoText.Text = $"共 {dataTable.Rows.Count} 条记录，{dataTable.Columns.Count} 列";
        }
    }
}
