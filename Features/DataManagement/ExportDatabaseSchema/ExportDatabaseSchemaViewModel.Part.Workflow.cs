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

        private bool CanStart()
        {
            return !IsProcessing &&
                   !string.IsNullOrWhiteSpace(InputGdbPath) &&
                   !string.IsNullOrWhiteSpace(OutputExcelPath);
        }
    }
}
