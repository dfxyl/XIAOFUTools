using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace XIAOFUTools.Features.DataManagement.MirrorDatabase
{
    public partial class MirrorDatabaseViewModel
    {

        private void Initialize()
        {
            _sourceDatabasePath = "";
            _outputFolderPath = "";
            _databaseName = "MirrorDatabase";
            _isProcessing = false;
            _logText = "";
        }

        private void InitializeCommands()
        {
            BrowseSourceDatabaseCommand = new RelayCommand(() => BrowseSourceDatabase(), () => !IsProcessing);
            BrowseOutputFolderCommand = new RelayCommand(() => BrowseOutputFolder(), () => !IsProcessing);
            StartCommand = new RelayCommand(() => StartMirrorDatabase(), () => CanStart());
            StopCommand = new RelayCommand(() => StopMirrorDatabase(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
        }

        private void MirrorFeatureClass(Geodatabase targetGdb, FeatureClassDefinition fcDef)
        {
            string fcName = fcDef.GetName();
            string fcAlias = fcDef.GetAliasName();

            var fieldDescriptions = new List<FieldDescription>();
            var fields = fcDef.GetFields();

            foreach (var field in fields)
            {
                string fieldName = field.Name;
                if (fieldName.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.Equals("Shape", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.Equals("Shape_Length", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.Equals("Shape_Area", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fieldDesc = new FieldDescription(fieldName, field.FieldType);
                fieldDesc.AliasName = field.AliasName;

                if (field.FieldType == FieldType.String)
                {
                    fieldDesc.Length = field.Length > 0 ? field.Length : 255;
                }

                fieldDescriptions.Add(fieldDesc);
            }

            var shapeType = fcDef.GetShapeType();
            var spatialRef = fcDef.GetSpatialReference();
            var shapeDescription = new ShapeDescription(shapeType, spatialRef);

            var fcDescription = new FeatureClassDescription(fcName, fieldDescriptions, shapeDescription);
            if (!string.IsNullOrEmpty(fcAlias) && fcAlias != fcName)
            {
                fcDescription = new FeatureClassDescription(fcName, fieldDescriptions, shapeDescription)
                {
                    AliasName = fcAlias
                };
            }

            var schemaBuilder = new SchemaBuilder(targetGdb);
            schemaBuilder.Create(fcDescription);

            if (schemaBuilder.Build())
            {
                LogInfo("  要素类 " + fcName + " 创建成功，包含 " + fieldDescriptions.Count + " 个字段");
            }
            else
            {
                var errorInfo = schemaBuilder.ErrorMessages;
                var errorMsg = errorInfo != null && errorInfo.Count > 0
                    ? string.Join("; ", errorInfo)
                    : "未知错误";
                LogError("  创建要素类 " + fcName + " 失败: " + errorMsg);
            }
        }

        private void MirrorFeatureClassInDataset(Geodatabase targetGdb, FeatureClassDefinition fcDef, string datasetName)
        {
            string fcName = fcDef.GetName();
            string fcAlias = fcDef.GetAliasName();

            var fieldDescriptions = new List<FieldDescription>();
            var fields = fcDef.GetFields();

            foreach (var field in fields)
            {
                string fieldName = field.Name;
                if (fieldName.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.Equals("Shape", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.Equals("Shape_Length", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.Equals("Shape_Area", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fieldDesc = new FieldDescription(fieldName, field.FieldType);
                fieldDesc.AliasName = field.AliasName;

                if (field.FieldType == FieldType.String)
                {
                    fieldDesc.Length = field.Length > 0 ? field.Length : 255;
                }

                fieldDescriptions.Add(fieldDesc);
            }

            var shapeType = fcDef.GetShapeType();
            var spatialRef = fcDef.GetSpatialReference();
            var shapeDescription = new ShapeDescription(shapeType, spatialRef);

            var fcDescription = new FeatureClassDescription(fcName, fieldDescriptions, shapeDescription);
            if (!string.IsNullOrEmpty(fcAlias) && fcAlias != fcName)
            {
                fcDescription = new FeatureClassDescription(fcName, fieldDescriptions, shapeDescription)
                {
                    AliasName = fcAlias
                };
            }

            var schemaBuilder = new SchemaBuilder(targetGdb);
            
            try
            {
                var datasetDef = targetGdb.GetDefinition<FeatureDatasetDefinition>(datasetName);
                var datasetToken = new FeatureDatasetDescription(datasetDef);
                schemaBuilder.Create(datasetToken, fcDescription);
            }
            catch
            {
                schemaBuilder.Create(fcDescription);
            }

            if (schemaBuilder.Build())
            {
                LogInfo("    要素类 " + fcName + " 创建成功（要素集: " + datasetName + "），包含 " + fieldDescriptions.Count + " 个字段");
            }
            else
            {
                var errorInfo = schemaBuilder.ErrorMessages;
                var errorMsg = errorInfo != null && errorInfo.Count > 0
                    ? string.Join("; ", errorInfo)
                    : "未知错误";
                LogError("    创建要素类 " + fcName + " 失败: " + errorMsg);
            }
        }

        private void MirrorTable(Geodatabase targetGdb, TableDefinition tableDef)
        {
            string tableName = tableDef.GetName();
            string tableAlias = tableDef.GetAliasName();

            var fieldDescriptions = new List<FieldDescription>();
            var fields = tableDef.GetFields();

            foreach (var field in fields)
            {
                string fieldName = field.Name;
                if (fieldName.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fieldDesc = new FieldDescription(fieldName, field.FieldType);
                fieldDesc.AliasName = field.AliasName;

                if (field.FieldType == FieldType.String)
                {
                    fieldDesc.Length = field.Length > 0 ? field.Length : 255;
                }

                fieldDescriptions.Add(fieldDesc);
            }

            var tableDescription = new TableDescription(tableName, fieldDescriptions);
            if (!string.IsNullOrEmpty(tableAlias) && tableAlias != tableName)
            {
                tableDescription = new TableDescription(tableName, fieldDescriptions)
                {
                    AliasName = tableAlias
                };
            }

            var schemaBuilder = new SchemaBuilder(targetGdb);
            schemaBuilder.Create(tableDescription);

            if (schemaBuilder.Build())
            {
                LogInfo("  表 " + tableName + " 创建成功，包含 " + fieldDescriptions.Count + " 个字段");
            }
            else
            {
                var errorInfo = schemaBuilder.ErrorMessages;
                var errorMsg = errorInfo != null && errorInfo.Count > 0
                    ? string.Join("; ", errorInfo)
                    : "未知错误";
                LogError("  创建表 " + tableName + " 失败: " + errorMsg);
            }
        }

        private void StopMirrorDatabase()
        {
            _cancellationTokenSource?.Cancel();
            LogWarning("正在停止操作...");
        }
    }
}
