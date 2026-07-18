using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using ArcGIS.Desktop.Editing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace XIAOFUTools.Features.DataManagement.RotateGeometry
{
    internal partial class RotateGeometryDockPaneViewModel
    {

        private static double Distance2D(MapPoint a, MapPoint b)
        {
            double dx = b.X - a.X; double dy = b.Y - a.Y; return Math.Sqrt(dx * dx + dy * dy);
        }

        private static MapPoint Interpolate(MapPoint a, MapPoint b, double t)
        {
            var sr = a.SpatialReference ?? b.SpatialReference;
            double x = a.X + (b.X - a.X) * t;
            double y = a.Y + (b.Y - a.Y) * t;
            return MapPointBuilderEx.CreateMapPoint(x, y, sr);
        }
        public void Cleanup()
        {
            if (_mapSelectionChangedToken != null)
            {
                MapSelectionChangedEvent.Unsubscribe(_mapSelectionChangedToken);
                _mapSelectionChangedToken = null;
            }
        }
    }
}
