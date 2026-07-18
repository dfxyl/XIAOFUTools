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
        public ObservableCollection<FeatureLayer> FeatureLayers
        {
            get => _featureLayers;
            set => SetProperty(ref _featureLayers, value);
        }
        public FeatureLayer SelectedLayer
        {
            get => _selectedLayer;
            set
            {
                if (SetProperty(ref _selectedLayer, value))
                {
                    NotifyPropertyChanged(() => HasSelectedLayer);
                    NotifyPropertyChanged(() => CanProcess);
                    LoadNumericFields();
                    UpdateSelectionInfo();
                    UpdateGeometryTypeFlags();
                    AppendLog($"已切换图层：{_selectedLayer?.Name ?? "(无)"}");
                }
            }
        }

        public bool HasSelectedLayer => SelectedLayer != null;
        public bool UseConstantAngle
        {
            get => _useConstantAngle;
            set
            {
                if (SetProperty(ref _useConstantAngle, value))
                {
                    if (value)
                    {
                        if (UseFieldAngle) UseFieldAngle = false;
                        AppendLog("角度模式：统一角度(度)");
                    }
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }
        public bool UseFieldAngle
        {
            get => _useFieldAngle;
            set
            {
                if (SetProperty(ref _useFieldAngle, value))
                {
                    if (value)
                    {
                        if (UseConstantAngle) UseConstantAngle = false;
                        AppendLog("角度模式：按字段角度(度)");
                    }
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }
        public double ConstantAngleDegrees
        {
            get => _constantAngleDegrees;
            set
            {
                if (SetProperty(ref _constantAngleDegrees, value))
                {
                    AppendLog($"统一角度(度)：{value}");
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }
        public string AngleDirection
        {
            get => _angleDirection;
            set
            {
                if (SetProperty(ref _angleDirection, value))
                {
                    AppendLog($"方向：{value}（逆时针为正，顺时针为负）");
                }
            }
        }
        public ObservableCollection<string> NumericFields
        {
            get => _numericFields;
            set => SetProperty(ref _numericFields, value);
        }
        public string SelectedAngleField
        {
            get => _selectedAngleField;
            set
            {
                if (SetProperty(ref _selectedAngleField, value))
                {
                    AppendLog($"角度字段：{value}");
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }
        public string LineAnchor
        {
            get => _lineAnchor;
            set
            {
                if (SetProperty(ref _lineAnchor, value))
                {
                    string msg = value == "起点" ? "线锚点：起点（以线首顶点为旋转中心）" :
                                  value == "中点" ? "线锚点：中点（沿折线总长度的半程点）" :
                                  value == "终点" ? "线锚点：终点（以线末顶点为旋转中心）" : $"线锚点：{value}";
                    AppendLog(msg);
                }
            }
        }
        public string PolygonAnchor
        {
            get => _polygonAnchor;
            set
            {
                if (SetProperty(ref _polygonAnchor, value))
                {
                    string msg = value == "质心" ? "面锚点：质心（几何质心）" :
                                  value == "标签点" ? "面锚点：标签点（内部代表点）" :
                                  value == "包络中心" ? "面锚点：包络中心（外包矩形中心）" :
                                  value == "左下角" ? "面锚点：左下角（外包矩形）" :
                                  value == "左上角" ? "面锚点：左上角（外包矩形）" :
                                  value == "右下角" ? "面锚点：右下角（外包矩形）" :
                                  value == "右上角" ? "面锚点：右上角（外包矩形）" :
                                  value == "起点" ? "面锚点：起点（外环第一段起点）" : $"面锚点：{value}";
                    AppendLog(msg);
                }
            }
        }
        public bool IsProcessing
        {
            get => _isProcessing;
            set { SetProperty(ref _isProcessing, value); NotifyPropertyChanged(() => CanProcess); }
        }

        public bool CanProcess
        {
            get
            {
                if (IsProcessing || !HasSelectedLayer)
                    return false;
                if (UseConstantAngle)
                    return true;
                if (UseFieldAngle)
                    return !string.IsNullOrEmpty(SelectedAngleField);
                return false;
            }
        }
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
        }
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }
        public string SelectionInfo
        {
            get => _selectionInfo;
            set => SetProperty(ref _selectionInfo, value);
        }
        public bool IsLineLayer
        {
            get => _isLineLayer;
            set => SetProperty(ref _isLineLayer, value);
        }
        public bool IsPolygonLayer
        {
            get => _isPolygonLayer;
            set => SetProperty(ref _isPolygonLayer, value);
        }
        public ICommand RefreshLayersCommand => new RelayCommand(() => RefreshLayers());
        public ICommand RunCommand => new RelayCommand(async () => await RunAsync(), () => CanProcess);
        public ICommand CancelCommand => new RelayCommand(() => Cancel(), () => IsProcessing);
        public ICommand ShowHelpCommand => new RelayCommand(() => ShowHelp());
    }
}
