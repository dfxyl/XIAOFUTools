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
        private string GetScaleCode(string scaleName)
        {
            return scaleName switch
            {
                "100万" => null,
                "50万" => "B",
                "25万" => "C",
                "10万" => "D",
                "5万"  => "E",
                "2.5万"=> "F",
                "1万"  => "G",
                "5千"  => "H",
                _ => null
            };
        }
        private (int rows, int cols) GetRowColCount(string code)
        {
            return code switch
            {
                "B" => (2,2),
                "C" => (4,4),
                "D" => (12,12),
                "E" => (24,24),
                "F" => (48,48),
                "G" => (96,96),
                "H" => (192,192),
                _ => (0,0)
            };
        }
        private (double xmin, double xmax, double ymin, double ymax) Get100kMapExtent(string mapCode)
        {
            char rowLetter = mapCode[0];
            int colNumber = int.Parse(mapCode.Substring(1));
            // 仅实现北半球（中国范围）标准：A 行起算于 0°N，向北每行 4°
            int rowIndex = rowLetter - 'A';
            if (rowIndex < 0) rowIndex = 0; // 防止非法字母
            double baseLat = rowIndex * 4.0;
            // 经差阈值：0–60°:6°，60–76°:12°，≥76°:24°
            double bandCenterLat = baseLat + 2.0;
            double lonStep = (Math.Abs(bandCenterLat) < 60.0) ? 6.0 : (Math.Abs(bandCenterLat) < 76.0 ? 12.0 : 24.0);
            double baseLon = (colNumber - 1) * lonStep - 180.0;
            double width = lonStep;
            double xmin = baseLon;
            double xmax = baseLon + width;
            double ymin = baseLat;
            double ymax = baseLat + 4.0;
            if (Math.Abs(baseLat) >= 88)
            {
                xmax = 180.0;
                ymax = baseLat >= 0 ? 90.0 : -90.0;
            }
            return (xmin, xmax, ymin, ymax);
        }
        private (double xmin, double xmax, double ymin, double ymax) GetExtentByScale(string mapCode, string scaleCode, int row, int col)
        {
            var e = Get100kMapExtent(mapCode);
            var (rows, cols) = GetRowColCount(scaleCode);
            if (rows <= 0 || cols <= 0)
                throw new ArgumentException($"不支持的比例尺代码: {scaleCode}");
            double parentLat = e.ymax - e.ymin;  // 通常为 4°
            double parentLon = e.xmax - e.xmin;  // 依纬带为 6°/12°/24°
            double latStep = parentLat / rows;
            double lonStep = parentLon / cols;
            double map_xmin = e.xmin + (col - 1) * lonStep;
            double map_xmax = e.xmin + col * lonStep;
            double map_ymin = e.ymax - row * latStep;
            double map_ymax = e.ymax - (row - 1) * latStep;
            return (map_xmin, map_xmax, map_ymin, map_ymax);
        }

        // 计算同基图幅（100k）的邻接TFH：用于scaleCode==null的情况
        private (string left, string right, string up, string low, string upl, string upr, string lowl, string lowr) GetNeighborsFor100k(string mapCode)
        {
            string left = GetAdjacent100k(mapCode, -1, 0);
            string right = GetAdjacent100k(mapCode, 1, 0);
            string up = GetAdjacent100k(mapCode, 0, 1);
            string low = GetAdjacent100k(mapCode, 0, -1);
            string upl = GetAdjacent100k(GetAdjacent100k(mapCode, -1, 0), 0, 1);
            string upr = GetAdjacent100k(GetAdjacent100k(mapCode, 1, 0), 0, 1);
            string lowl = GetAdjacent100k(GetAdjacent100k(mapCode, -1, 0), 0, -1);
            string lowr = GetAdjacent100k(GetAdjacent100k(mapCode, 1, 0), 0, -1);
            return (left, right, up, low, upl, upr, lowl, lowr);
        }

        // 计算任意比例尺下的邻接TFH（同尺度）。当越界时切换到相邻100k并做行列回卷
        private (string left, string right, string up, string low, string upl, string upr, string lowl, string lowr) GetNeighborsForScale(string base100k, string scaleCode, int r, int c)
        {
            var (rows, cols) = GetRowColCount(scaleCode);
            // 左右
            string leftBase = base100k; int lc = c - 1; int lr = r;
            if (lc < 1) { lc = cols; leftBase = GetAdjacent100k(base100k, -1, 0); }
            string rightBase = base100k; int rc = c + 1; int rr0 = r;
            if (rc > cols) { rc = 1; rightBase = GetAdjacent100k(base100k, 1, 0); }
            // 上下
            string upBase = base100k; int ur = r - 1; int uc = c;
            if (ur < 1) { ur = rows; upBase = GetAdjacent100k(base100k, 0, 1); }
            string lowBase = base100k; int dr = r + 1; int dc = c;
            if (dr > rows) { dr = 1; lowBase = GetAdjacent100k(base100k, 0, -1); }

            string left = $"{leftBase}{scaleCode}{lr:000}{lc:000}";
            string right = $"{rightBase}{scaleCode}{rr0:000}{rc:000}";
            string up = $"{upBase}{scaleCode}{ur:000}{uc:000}";
            string low = $"{lowBase}{scaleCode}{dr:000}{dc:000}";

            // 角
            var (ulBase, ulr, ulc) = (upBase, ur, uc - 1); if (ulc < 1) { ulc = cols; ulBase = GetAdjacent100k(upBase, -1, 0); }
            var (urBase, urr, urc) = (upBase, ur, uc + 1); if (urc > cols) { urc = 1; urBase = GetAdjacent100k(upBase, 1, 0); }
            var (dlBase, dlr, dlc) = (lowBase, dr, dc - 1); if (dlc < 1) { dlc = cols; dlBase = GetAdjacent100k(lowBase, -1, 0); }
            var (drBase, drr, drc) = (lowBase, dr, dc + 1); if (drc > cols) { drc = 1; drBase = GetAdjacent100k(lowBase, 1, 0); }

            string upl = $"{ulBase}{scaleCode}{ulr:000}{ulc:000}";
            string upr = $"{urBase}{scaleCode}{urr:000}{urc:000}";
            string lowl = $"{dlBase}{scaleCode}{dlr:000}{dlc:000}";
            string lowr = $"{drBase}{scaleCode}{drr:000}{drc:000}";

            return (left, right, up, low, upl, upr, lowl, lowr);
        }

        // 求相邻100k图幅代码，dx: -1左/1右, dy: -1下/1上
        private string GetAdjacent100k(string mapCode, int dx, int dy)
        {
            var e = Get100kMapExtent(mapCode);
            double width = e.xmax - e.xmin;
            double height = e.ymax - e.ymin; // 应为4°
            double cx = (e.xmin + e.xmax) / 2.0 + dx * width;
            double cy = (e.ymin + e.ymax) / 2.0 + dy * height;
            cx = NormalizeLon(cx);
            cy = Math.Max(-90, Math.Min(90, cy));
            return Get100kCodeByLatLon(cy, cx);
        }

        // 由纬度/经度返回100k图幅代码
        private string Get100kCodeByLatLon(double lat, double lon)
        {
            if (lat < 0) lat = 0; // 假定北半球
            int rowIndex = (int)Math.Floor(lat / 4.0);
            char rowLetter = (char)('A' + rowIndex);
            double stepLon = (Math.Abs(lat) < 60.0) ? 6.0 : (Math.Abs(lat) < 76.0 ? 12.0 : 24.0);
            int colIndex = (int)Math.Floor((lon + 180.0) / stepLon);
            int colNumber = colIndex + 1;
            return $"{rowLetter}{colNumber}";
        }
    }
}
