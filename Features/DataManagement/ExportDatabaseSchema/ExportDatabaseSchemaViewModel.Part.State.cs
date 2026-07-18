using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;

namespace XIAOFUTools.Features.DataManagement.ExportDatabaseSchema
{
    public partial class ExportDatabaseSchemaViewModel
    {

        /// <summary>
        /// 输入GDB路径
        /// </summary>
        public string InputGdbPath
        {
            get => _inputGdbPath;
            set => SetProperty(ref _inputGdbPath, value);
        }

        /// <summary>
        /// 输出Excel路径
        /// </summary>
        public string OutputExcelPath
        {
            get => _outputExcelPath;
            set => SetProperty(ref _outputExcelPath, value);
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

        public ICommand BrowseInputGdbCommand { get; private set; }
        public ICommand BrowseOutputExcelCommand { get; private set; }
        public ICommand StartCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }
    }
}
