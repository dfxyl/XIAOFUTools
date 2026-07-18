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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Features.DataManagement.ExportShpFieldTable.Core;

namespace XIAOFUTools.Features.DataManagement.ExportShpFieldTable
{
    public partial class ExportShpFieldTableViewModel
    {

        private string ConvertGeometryType(GeometryType geometryType)
        {
            return geometryType switch
            {
                GeometryType.Point => "POINT",
                GeometryType.Polyline => "POLYLINE",
                GeometryType.Polygon => "POLYGON",
                GeometryType.Multipoint => "MULTIPOINT",
                _ => geometryType.ToString().ToUpperInvariant()
            };
        }

        private string ConvertFieldType(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.String => "TEXT",
                FieldType.SmallInteger => "SHORT",
                FieldType.Integer => "LONG",
                FieldType.Single => "FLOAT",
                FieldType.Double => "DOUBLE",
                FieldType.Date => "DATE",
                FieldType.Blob => "BLOB",
                FieldType.GUID => "GUID",
                FieldType.GlobalID => "GUID",
                _ => fieldType.ToString().ToUpperInvariant()
            };
        }
    }
}
