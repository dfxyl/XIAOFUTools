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

        private static double ComputeSignedArea(IReadOnlyList<MapPoint> points)
        {
            var count = points.Count;
            if (count < 3)
                return 0;

            if (count > 1 &&
                Math.Abs(points[0].X - points[count - 1].X) < 1e-12 &&
                Math.Abs(points[0].Y - points[count - 1].Y) < 1e-12)
            {
                count--;
            }

            if (count < 3)
                return 0;

            double sum = 0;
            for (var i = 0; i < count; i++)
            {
                var current = points[i];
                var next = points[(i + 1) % count];
                sum += current.X * next.Y - next.X * current.Y;
            }

            return sum * 0.5;
        }
    }
}
