using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.TxtToFeature
{
    public partial class TxtToFeatureDockPaneViewModel
    {

        /// <summary>
        /// 输入文件夹
        /// </summary>
        public string InputFolder
        {
            get => _inputFolder;
            set => SetProperty(ref _inputFolder, value);
        }

        /// <summary>
        /// 输出文件夹
        /// </summary>
        public string OutputFolder
        {
            get => _outputFolder;
            set => SetProperty(ref _outputFolder, value);
        }

        /// <summary>
        /// 字段名称
        /// </summary>
        public string FieldNames
        {
            get => _fieldNames;
            set => SetProperty(ref _fieldNames, value);
        }

        /// <summary>
        /// 单独文件夹
        /// </summary>
        public bool SeparateFolder
        {
            get => _separateFolder;
            set => SetProperty(ref _separateFolder, value);
        }

        /// <summary>
        /// 合并到一个文件
        /// </summary>
        public bool MergeToOneFile
        {
            get => _mergeToOneFile;
            set => SetProperty(ref _mergeToOneFile, value);
        }

        /// <summary>
        /// XY互换
        /// </summary>
        public bool SwapXY
        {
            get => _swapXY;
            set => SetProperty(ref _swapXY, value);
        }

        /// <summary>
        /// 保存在TXT源路径
        /// </summary>
        public bool SaveToSourcePath
        {
            get => _saveToSourcePath;
            set
            {
                SetProperty(ref _saveToSourcePath, value);
                OnPropertyChanged(nameof(ShowOutputFolder));
            }
        }

        /// <summary>
        /// 是否显示输出文件夹组件
        /// </summary>
        public bool ShowOutputFolder => !SaveToSourcePath;

        /// <summary>
        /// 遍历子文件夹
        /// </summary>
        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set => SetProperty(ref _includeSubfolders, value);
        }

        /// <summary>
        /// 选择的坐标系
        /// </summary>
        public SpatialReference SelectedSpatialReference
        {
            get => _selectedSpatialReference;
            set
            {
                SetProperty(ref _selectedSpatialReference, value);
                SelectedCoordinateSystemName = value?.Name ?? "未选择坐标系";
            }
        }

        /// <summary>
        /// 选择的坐标系名称
        /// </summary>
        public string SelectedCoordinateSystemName
        {
            get => _selectedCoordinateSystemName;
            set => SetProperty(ref _selectedCoordinateSystemName, value);
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        /// <summary>
        /// 进度值
        /// </summary>
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        /// <summary>
        /// 进度是否不确定
        /// </summary>
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        /// <summary>
        /// 日志文本
        /// </summary>
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// 是否请求取消
        /// </summary>
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
        }

        /// <summary>
        /// 选择输入文件夹命令
        /// </summary>
        public ICommand SelectInputFolderCommand { get; }

        /// <summary>
        /// 选择输出文件夹命令
        /// </summary>
        public ICommand SelectOutputFolderCommand { get; }

        /// <summary>
        /// 选择坐标系命令
        /// </summary>
        public ICommand SelectCoordinateSystemCommand { get; }

        /// <summary>
        /// 开始转换命令
        /// </summary>
        public ICommand StartConversionCommand { get; }

        /// <summary>
        /// 停止转换命令
        /// </summary>
        public ICommand StopConversionCommand { get; }

        /// <summary>
        /// 帮助命令
        /// </summary>
        public ICommand HelpCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
