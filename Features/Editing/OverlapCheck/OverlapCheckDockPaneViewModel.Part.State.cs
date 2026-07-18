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
    internal partial class OverlapCheckDockPaneViewModel
    {
        /// <summary>
        /// 面要素图层列表
        /// </summary>
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }

        /// <summary>
        /// 选中的面要素图层
        /// </summary>
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                UpdateOutputPath();
                NotifyPropertyChanged(nameof(CanProcess));
                NotifyPropertyChanged(nameof(HasSelectedLayer));
            }
        }

        /// <summary>
        /// 容差值
        /// </summary>
        public string Tolerance
        {
            get => _tolerance;
            set => SetProperty(ref _tolerance, value);
        }

        /// <summary>
        /// 输出路径
        /// </summary>
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                // 使用通用工具规范化输出路径（自动识别 GDB/SHP）
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_重叠区域" : "重叠区域";
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, defName);
                SetProperty(ref _outputPath, normalized);
            }
        }

        /// <summary>
        /// 日志内容
        /// </summary>
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// 进度值
        /// </summary>
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        /// <summary>
        /// 进度条是否为不确定状态
        /// </summary>
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        /// <summary>
        /// 是否可以开始处理
        /// </summary>
        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrWhiteSpace(OutputPath);

        /// <summary>
        /// 是否有选中图层
        /// </summary>
        public bool HasSelectedLayer => SelectedPolygonLayer != null;
        public List<string> SelectedFields
        {
            get => _selectedFields;
            set
            {
                _selectedFields = value ?? new List<string>();
                SetProperty(ref _selectedFields, value);
                NotifyPropertyChanged(nameof(SelectedFieldsDisplayText));
            }
        }

        /// <summary>
        /// 选中字段的显示文本
        /// </summary>
        public string SelectedFieldsDisplayText
        {
            get
            {
                if (SelectedFields == null || SelectedFields.Count == 0)
                {
                    return "未选择字段";
                }
                return $"已选择 {SelectedFields.Count} 个字段";
            }
        }
        public ICommand BrowseOutputCommand { get; private set; }
        public ICommand RunCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }
        public ICommand SelectFieldsCommand { get; private set; }
        public ICommand RefreshLayersCommand { get; private set; }
    }
}
