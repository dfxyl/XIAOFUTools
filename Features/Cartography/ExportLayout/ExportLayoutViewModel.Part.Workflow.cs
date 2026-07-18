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

        /// <summary>
        /// 是否可以开始导出
        /// </summary>
        private bool CanStart()
        {
            return !IsRunning && 
                   !string.IsNullOrWhiteSpace(OutputFolder) && 
                   Layouts.Any(l => l.IsSelected) &&
                   int.TryParse(Resolution, out int res) && res > 0;
        }
    }
}
