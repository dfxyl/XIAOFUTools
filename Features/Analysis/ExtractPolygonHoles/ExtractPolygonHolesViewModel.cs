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
    internal partial class ExtractPolygonHolesViewModel : PropertyChangedBase
    {
        private ObservableCollection<FeatureLayer> _polygonLayers = new();
        private FeatureLayer _selectedPolygonLayer;
        private string _outputPath = string.Empty;
        private bool _createMultipartOutput;
        private string _logContent = string.Empty;
        private string _statusMessage = "准备就绪";
        private int _progress;
        private bool _isProcessing;
        private CancellationTokenSource _cts;

        public ExtractPolygonHolesViewModel()
        {
            RefreshLayers();
            UpdateDefaultOutputPath();
        }
    }
}
