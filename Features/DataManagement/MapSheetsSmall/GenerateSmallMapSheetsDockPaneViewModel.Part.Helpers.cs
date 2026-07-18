using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using XIAOFUTools.Shared;
using ArcGIS.Desktop.Framework.Dialogs;

namespace XIAOFUTools.Features.DataManagement.MapSheetsSmall
{
    internal partial class GenerateSmallMapSheetsDockPaneViewModel
    {

        private void MessageBox(string msg) => PresentationServices.Dialogs.Show(msg, "提示");
        private string[] Compute100kCodes(double xmin, double xmax, double ymin, double ymax)
        {
            var codes = new System.Collections.Generic.HashSet<string>();
            if (ymin < 0) ymin = 0; // 中国范围假定北半球
            var (yStart, yEnd) = MapSheetCoverageUtils.GetInclusiveIndexRange(ymin, ymax, 4.0);
            for (int y = yStart; y <= yEnd; y++)
            {
                double bandLat = y * 4.0 + 2.0; // 中纬
                double lonStep = (bandLat < 60.0) ? 6.0 : (bandLat < 76.0 ? 12.0 : 24.0);
                var (xStart, xEnd) = MapSheetCoverageUtils.GetInclusiveIndexRange(xmin + 180.0, xmax + 180.0, lonStep);
                for (int x = xStart; x <= xEnd; x++)
                {
                    char rowLetter = (char)('A' + y);
                    string code = $"{rowLetter}{x + 1}";
                    codes.Add(code);
                }
            }
            return codes.ToArray();
        }
    }
}
