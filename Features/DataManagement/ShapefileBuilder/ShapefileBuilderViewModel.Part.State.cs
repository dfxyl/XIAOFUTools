using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ExcelDataReader;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.ShapefileBuilder
{
    public partial class ShapefileBuilderViewModel
    {

        /// <summary>
        /// 输入Excel文件路径
        /// </summary>
        public string InputExcelPath
        {
            get => _inputExcelPath;
            set => SetProperty(ref _inputExcelPath, value);
        }

        /// <summary>
        /// 输出目录路径
        /// </summary>
        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set => SetProperty(ref _outputFolderPath, value);
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
        /// 日志文本
        /// </summary>
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        /// <summary>
        /// 选择的坐标系
        /// </summary>
        public SpatialReference SelectedSpatialReference
        {
            get => _selectedSpatialReference;
            set
            {
                if (SetProperty(ref _selectedSpatialReference, value))
                {
                    SelectedCoordinateSystemName = value == null
                        ? "未选择坐标系"
                        : $"{value.Name} (WKID: {value.Wkid})";
                }
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
        /// 浏览输入Excel命令
        /// </summary>
        public ICommand BrowseInputExcelCommand { get; private set; }

        /// <summary>
        /// 浏览输出目录命令
        /// </summary>
        public ICommand BrowseOutputFolderCommand { get; private set; }

        /// <summary>
        /// 导出模板命令
        /// </summary>
        public ICommand ExportTemplateCommand { get; private set; }

        /// <summary>
        /// 选择坐标系命令
        /// </summary>
        public ICommand SelectCoordinateSystemCommand { get; private set; }

        /// <summary>
        /// 开始执行命令
        /// </summary>
        public ICommand StartCommand { get; private set; }

        /// <summary>
        /// 停止执行命令
        /// </summary>
        public ICommand StopCommand { get; private set; }

        /// <summary>
        /// 帮助命令
        /// </summary>
        public ICommand ShowHelpCommand { get; private set; }
    }
}
