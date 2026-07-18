using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;
using ExcelDataReader;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.DatabaseBuilder
{
    public partial class DatabaseBuilderViewModel
    {

        /// <summary>
        /// 输入Excel文件路径
        /// </summary>
        public string InputExcelPath
        {
            get { return _inputExcelPath; }
            set
            {
                SetProperty(ref _inputExcelPath, value);
            }
        }

        /// <summary>
        /// 输出文件夹路径
        /// </summary>
        public string OutputFolderPath
        {
            get { return _outputFolderPath; }
            set
            {
                SetProperty(ref _outputFolderPath, value);
            }
        }

        /// <summary>
        /// 数据库名称
        /// </summary>
        public string DatabaseName
        {
            get { return _databaseName; }
            set
            {
                SetProperty(ref _databaseName, value);
            }
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get { return _isProcessing; }
            set
            {
                SetProperty(ref _isProcessing, value);
            }
        }

        /// <summary>
        /// 日志文本
        /// </summary>
        public string LogText
        {
            get { return _logText; }
            set
            {
                SetProperty(ref _logText, value);
            }
        }

        /// <summary>
        /// 选择的坐标系
        /// </summary>
        public SpatialReference SelectedSpatialReference
        {
            get { return _selectedSpatialReference; }
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
            get { return _selectedCoordinateSystemName; }
            set
            {
                SetProperty(ref _selectedCoordinateSystemName, value);
            }
        }

        /// <summary>
        /// 浏览输入Excel命令
        /// </summary>
        public ICommand BrowseInputExcelCommand { get; private set; }

        /// <summary>
        /// 浏览输出文件夹命令
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
