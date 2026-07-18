using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    internal sealed partial class LandClassTableDockPaneViewModel
    {

        private sealed record RedlineItem(string ProjectName, double TotalArea, Polygon Geometry, string GroupValue, string PlotName);


        private sealed class UnmatchedLandClassInfo
        {
            public UnmatchedLandClassInfo(string displayValue)
            {
                DisplayValue = displayValue;
            }

            public string DisplayValue { get; }
            public int Count { get; set; }
            public List<string> Locations { get; } = new();
        }


    }
}
