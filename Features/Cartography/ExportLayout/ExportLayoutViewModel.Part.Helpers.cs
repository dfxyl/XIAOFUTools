using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Cartography.ExportLayout
{
    public partial class ExportLayoutViewModel
    {

        private void InitializeCommands()
        {
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            SelectAllCommand = new RelayCommand(SelectAll);
            InvertSelectionCommand = new RelayCommand(InvertSelection);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            StopCommand = new RelayCommand(Stop);
            StartCommand = new RelayCommand(async () => await StartExport(), CanStart);
            RefreshLayoutsCommand = new RelayCommand(RefreshLayouts);
        }

        /// <summary>
        /// 停止导出
        /// </summary>
        private void Stop()
        {
            _cancellationTokenSource?.Cancel();
            IsRunning = false;
        }
    }
}
