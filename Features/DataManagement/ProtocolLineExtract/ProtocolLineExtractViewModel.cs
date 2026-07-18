using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
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
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.ProtocolLineExtract
{
    /// <summary>
    /// 提取协议线 视图模型
    /// </summary>
    internal partial class ProtocolLineExtractViewModel : PropertyChangedBase
    {
        private readonly Infrastructure.ProtocolLineTemporaryWorkspaceStore _temporaryWorkspaceStore = new Infrastructure.ProtocolLineTemporaryWorkspaceStore();
        private dynamic _mapSelectionChangedToken;

        // 为规避个别环境下移除图层触发的 ArcGIS Pro 内部渲染 NRE，可按需关闭/开启
        private const bool RemoveIntermediatesFromMap = true;
        private ObservableCollection<FeatureLayer> _polygonLayers = new();

        private FeatureLayer _selectedPolygonLayer;

        private string _outputPath = string.Empty;

        private bool _mergeLines = true;

        // 选择集相关
        private bool _useSelection = true;

        private bool _hasSelection;

        private int _selectedCount;

        private string _selectionInfoText;

        // 保留字段
        private List<string> _selectedFields = new();

        private string _logContent = string.Empty;

        private string _statusMessage = "准备就绪";

        private int _progress;

        private bool _isProgressIndeterminate;

        private bool _isProcessing;

        private CancellationTokenSource _cts;

        public ProtocolLineExtractViewModel()
        {
            RefreshLayers();
            UpdateDefaultOutputPath();
            _mapSelectionChangedToken = MapSelectionChangedEvent.Subscribe(_ => UpdateSelectionInfo());
            UpdateSelectionInfo();
        }
    }
}
