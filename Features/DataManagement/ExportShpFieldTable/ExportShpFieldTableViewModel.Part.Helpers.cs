using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace XIAOFUTools.Features.DataManagement.ExportShpFieldTable
{
    public partial class ExportShpFieldTableViewModel
    {

        private bool IsSystemField(string fieldName)
        {
            var systemFields = new[]
            {
                "OBJECTID",
                "OID",
                "FID",
                "SHAPE",
                "Shape",
                "Shape_Length",
                "Shape_Area",
                "Shape_Leng"
            };
            return systemFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
        }

    }
}
