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
    /// <summary>
    /// 输出数据库属性结构表视图模型
    /// </summary>
    public partial class ExportDatabaseSchemaViewModel : PropertyChangedBase
    {
        private readonly Infrastructure.DatabaseSchemaPathStore _pathStore = new Infrastructure.DatabaseSchemaPathStore();

        private string _inputGdbPath;
        private string _outputExcelPath;
        private bool _isProcessing;
        private string _logText;
        private CancellationTokenSource _cancellationTokenSource;

        public ExportDatabaseSchemaViewModel()
        {
            Initialize();
            InitializeCommands();
        }
    }
}
