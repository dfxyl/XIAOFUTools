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
    internal partial class GenerateSmallMapSheetsDockPaneViewModel : PropertyChangedBase
    {
        private FeatureLayer _selectedPolygonLayer;
        private string _selectedScaleName = "10万";

        private string _outputFeatureClassPath = "";

        private bool _useLayerExtent = true;

        private bool _isProcessing;

        private string _logContent = "";

        private Envelope _drawnExtent; // 在当前地图坐标系下

        // 范围模式：Layer / Map / Custom
        private string _selectedRangeMode = "Layer";

        public GenerateSmallMapSheetsDockPaneViewModel()
        {
            RefreshLayers();
            if (string.IsNullOrWhiteSpace(OutputFeatureClassPath))
            {
                var gdb = Project.Current?.DefaultGeodatabasePath;
                if (!string.IsNullOrEmpty(gdb))
                    OutputFeatureClassPath = Path.Combine(gdb, $"MapSheets_{DateTime.Now:yyyyMMdd_HHmmss}");
            }
            // 订阅框选事件
            CustomExtentTool.ExtentCreatedStatic -= OnExtentCreated;
            CustomExtentTool.ExtentCreatedStatic += OnExtentCreated;
        }
    }
}
