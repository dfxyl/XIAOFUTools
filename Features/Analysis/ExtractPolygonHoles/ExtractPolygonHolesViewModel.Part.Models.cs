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

        private sealed class ExtractionStats
        {
            public int SourceFeatureCount { get; set; }
            public int HoleRingCount { get; set; }
            public int OutputFeatureCount { get; set; }
        }

        private sealed class RingGeometry
        {
            public RingGeometry(Polygon ringPolygon, double signedArea)
            {
                RingPolygon = ringPolygon;
                SignedArea = signedArea;
            }

            public Polygon RingPolygon { get; }
            public double SignedArea { get; }
        }

        private sealed class FieldMapping
        {
            public FieldMapping(string sourceFieldName, string outputFieldName)
            {
                SourceFieldName = sourceFieldName;
                OutputFieldName = outputFieldName;
            }

            public string SourceFieldName { get; }
            public string OutputFieldName { get; }
        }
    }
}
