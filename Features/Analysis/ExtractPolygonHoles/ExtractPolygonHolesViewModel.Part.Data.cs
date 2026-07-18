using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Analysis.ExtractPolygonHoles
{
    internal partial class ExtractPolygonHolesViewModel
    {

        private string GetDefaultOutputName()
        {
            return SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_扣岛" : "提取面扣岛";
        }

        private static List<MapPoint> GetPartPoints(ReadOnlySegmentCollection part)
        {
            var points = new List<MapPoint>();
            if (part == null || part.Count == 0)
                return points;

            foreach (var segment in part)
                points.Add(segment.StartPoint);

            points.Add(part[part.Count - 1].EndPoint);
            return points;
        }
    }
}
