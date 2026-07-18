using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.DataPivot
{
    internal partial class DataPivotDockPaneViewModel
    {

        public void ResetOutputToProjectDefaultGdb()
        {
            var defaultGdb = GetProjectDefaultGdbPath();
            if (!string.IsNullOrWhiteSpace(defaultGdb))
                OutputGdbPath = defaultGdb;
        }

        private static bool IsUnsupportedField(FieldType fieldType)
        {
            return fieldType == FieldType.Geometry ||
                   fieldType == FieldType.Blob ||
                   fieldType == FieldType.Raster ||
                   fieldType == FieldType.XML;
        }

        private static bool IsValueFieldSupported(FieldType fieldType)
        {
            return fieldType != FieldType.Geometry &&
                   fieldType != FieldType.Blob &&
                   fieldType != FieldType.Raster &&
                   fieldType != FieldType.XML &&
                   fieldType != FieldType.OID &&
                   fieldType != FieldType.GlobalID;
        }

        private static bool IsNumericField(PivotFieldOption option)
        {
            if (option == null)
                return false;

            return option.FieldType == FieldType.Double ||
                   option.FieldType == FieldType.Single ||
                   option.FieldType == FieldType.Integer ||
                   option.FieldType == FieldType.SmallInteger ||
                   option.FieldType == FieldType.BigInteger;
        }

        private async Task DeleteTempTableAsync(string tablePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tablePath))
                    return;

                var deleteParams = Geoprocessing.MakeValueArray(tablePath);
                await Geoprocessing.ExecuteToolAsync("management.Delete", deleteParams);
            }
            catch
            {
                // 清理失败可忽略
            }
        }
    }
}
