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
using XIAOFUTools.Features.DataManagement.ExportDatabaseSchema.Core;

namespace XIAOFUTools.Features.DataManagement.ExportDatabaseSchema
{
    public partial class ExportDatabaseSchemaViewModel
    {

        private FeatureClassInfo ReadFeatureClassInfo(FeatureClassDefinition fcDef, string featureDataset)
        {
            var fcInfo = new FeatureClassInfo
            {
                Name = fcDef.GetName(),
                AliasName = fcDef.GetAliasName(),
                GeometryType = fcDef.GetShapeType().ToString(),
                FeatureDataset = featureDataset ?? ""
            };

            // 读取字段
            var fields = fcDef.GetFields();
            foreach (var field in fields)
            {
                // 跳过系统字段
                if (IsSystemField(field.Name)) continue;

                fcInfo.Fields.Add(new FieldInfo
                {
                    FieldName = field.Name,
                    AliasName = field.AliasName,
                    FieldType = ConvertFieldType(field.FieldType),
                    Length = field.FieldType == FieldType.String ? field.Length : null,
                    Precision = field.Precision > 0 ? field.Precision : null,
                    Scale = field.Scale > 0 ? field.Scale : null,
                    IsNullable = field.IsNullable,
                    DefaultValue = field.GetDefaultValue()?.ToString() ?? ""
                });
            }

            return fcInfo;
        }

        private FeatureClassInfo ReadTableInfo(TableDefinition tableDef)
        {
            var tableInfo = new FeatureClassInfo
            {
                Name = tableDef.GetName(),
                AliasName = tableDef.GetAliasName(),
                GeometryType = "Table",
                FeatureDataset = ""
            };

            var fields = tableDef.GetFields();
            foreach (var field in fields)
            {
                if (IsSystemField(field.Name)) continue;

                tableInfo.Fields.Add(new FieldInfo
                {
                    FieldName = field.Name,
                    AliasName = field.AliasName,
                    FieldType = ConvertFieldType(field.FieldType),
                    Length = field.FieldType == FieldType.String ? field.Length : null,
                    Precision = field.Precision > 0 ? field.Precision : null,
                    Scale = field.Scale > 0 ? field.Scale : null,
                    IsNullable = field.IsNullable,
                    DefaultValue = field.GetDefaultValue()?.ToString() ?? ""
                });
            }

            return tableInfo;
        }

        private string ConvertFieldType(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.String => "Text",
                FieldType.SmallInteger => "Short",
                FieldType.Integer => "Long",
                FieldType.Single => "Float",
                FieldType.Double => "Double",
                FieldType.Date => "Date",
                FieldType.Blob => "Blob",
                FieldType.GUID => "GUID",
                FieldType.GlobalID => "GlobalID",
                FieldType.OID => "OID",
                _ => fieldType.ToString()
            };
        }
    }
}
