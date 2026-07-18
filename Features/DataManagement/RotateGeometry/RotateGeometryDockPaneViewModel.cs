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
    internal partial class RotateGeometryDockPaneViewModel : PropertyChangedBase
    {
        private dynamic _mapSelectionChangedToken;
        private ObservableCollection<FeatureLayer> _featureLayers = new ObservableCollection<FeatureLayer>();

        private FeatureLayer _selectedLayer;

        // 自动根据是否存在选择集决定处理范围，无需用户勾选

        private bool _useConstantAngle = true;

        private bool _useFieldAngle = false;

        private double _constantAngleDegrees = 0.0;

        private string _angleDirection = "逆时针"; // 统一方向设置：逆时针为正、顺时针为负

        private ObservableCollection<string> _numericFields = new ObservableCollection<string>();

        private string _selectedAngleField;

        // 删除字段正方向，统一使用 AngleDirection

        private string _lineAnchor = "中点"; // 起点/中点/终点

        private string _polygonAnchor = "质心"; // 质心/起点

        private bool _isProcessing = false;

        private int _progress = 0;

        private bool _isProgressIndeterminate = false;

        private bool _cancelRequested = false;

        private string _statusMessage = "就绪";

        private string _logContent = string.Empty;

        private string _selectionInfo = "未选择要素，将处理全部";
        private int _lastSelectionCount = -1;

        // 根据当前图层类型控制锚点UI显示
        private bool _isLineLayer;

        private bool _isPolygonLayer;
        public RotateGeometryDockPaneViewModel()
        {
            RefreshLayers();
            _mapSelectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
        }
    }

    /// <summary>
    /// 简易命令实现（本命名空间内独立一份，避免跨命名空间引用）
    /// </summary>

}
