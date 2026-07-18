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

        private string GetAreaFieldName()
        {
            return _areaUnit switch
            {
                "平方米" => "Area_M2",
                "公顷" => "Area_Ha",
                "亩" => "Area_Mu",
                _ => "Area_M2"
            };
        }

        private double ConvertAreaUnit(double areaInSquareMeters)
        {
            return _areaUnit switch
            {
                "平方米" => areaInSquareMeters,
                "公顷" => areaInSquareMeters / 10000.0,
                "亩" => areaInSquareMeters / 666.6666666667,
                _ => areaInSquareMeters
            };
        }
    }
}
