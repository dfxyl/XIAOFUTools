using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.IO;
using System.Linq;

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.OverlapCheck
{
    /// <summary>
    /// 图形重叠检查工具停靠窗格视图模型
    /// </summary>
    internal partial class OverlapCheckDockPaneViewModel : PropertyChangedBase
    {
        private const string _dockPaneID = "XIAOFUTools_OverlapCheckDockPane";
        private ObservableCollection<FeatureLayer> _polygonLayers;
        private FeatureLayer _selectedPolygonLayer;
        private string _tolerance = "0.001";
        private string _outputPath = "";
        private string _logContent = "";
        private string _statusMessage = "准备就绪";
        private int _progress = 0;
        private bool _isProgressIndeterminate = false;
        private bool _isProcessing = false;
        private CancellationTokenSource _cancellationTokenSource;
        public OverlapCheckDockPaneViewModel()
        {
            InitializeCommands();
            InitializeData();
        }

        /// <summary>
        /// 选中的保留字段
        /// </summary>
        private List<string> _selectedFields = new List<string>();


    }
}
