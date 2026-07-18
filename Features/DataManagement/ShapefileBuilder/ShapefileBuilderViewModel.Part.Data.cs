using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ExcelDataReader;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.ShapefileBuilder
{
    public partial class ShapefileBuilderViewModel
    {

        /// <summary>
        /// 读取Excel文件为DataSet
        /// </summary>
        private DataSet ReadExcelToDataSet(string excelPath)
        {
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                using (var fs = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    IExcelDataReader reader;
                    if (excelPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        reader = ExcelReaderFactory.CreateOpenXmlReader(fs);
                    }
                    else
                    {
                        reader = ExcelReaderFactory.CreateBinaryReader(fs);
                    }

                    using (reader)
                    {
                        var config = new ExcelDataSetConfiguration
                        {
                            ConfigureDataTable = _ => new ExcelDataTableConfiguration
                            {
                                UseHeaderRow = true
                            }
                        };

                        return reader.AsDataSet(config);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"读取Excel文件失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 读取图层表
        /// </summary>
        private List<LayerDefinition> ReadLayersTable(DataSet dataSet)
        {
            var layers = new List<LayerDefinition>();

            try
            {
                DataTable sheet = null;
                foreach (DataTable table in dataSet.Tables)
                {
                    if (table.TableName == "图层")
                    {
                        sheet = table;
                        break;
                    }
                }

                if (sheet == null)
                {
                    LogError("Excel中未找到'图层'工作表");
                    return null;
                }

                foreach (DataRow row in sheet.Rows)
                {
                    var layer = new LayerDefinition
                    {
                        Index = GetCellIntValue(row, 0),
                        LayerAlias = GetCellStringValue(row, 1),
                        GeometryType = GetCellStringValue(row, 2),
                        AttributesTable = GetCellStringValue(row, 3),
                        FeatureDataset = GetCellStringValue(row, 4),
                        Constraint = GetCellStringValue(row, 5),
                        Notes = GetCellStringValue(row, 6)
                    };

                    if (!string.IsNullOrEmpty(layer.AttributesTable) && !string.IsNullOrEmpty(layer.GeometryType))
                    {
                        layers.Add(layer);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"读取图层表失败: {ex.Message}");
                return null;
            }

            return layers;
        }

        /// <summary>
        /// 读取要素集表
        /// </summary>
        private List<FeatureDatasetInfo> ReadFeatureDatasetsTable(DataSet dataSet)
        {
            var datasets = new List<FeatureDatasetInfo>();

            try
            {
                DataTable sheet = null;
                foreach (DataTable table in dataSet.Tables)
                {
                    if (table.TableName == "要素集")
                    {
                        sheet = table;
                        break;
                    }
                }

                if (sheet == null)
                {
                    return datasets;
                }

                foreach (DataRow row in sheet.Rows)
                {
                    var dataset = new FeatureDatasetInfo
                    {
                        Index = GetCellIntValue(row, 0),
                        DatasetName = GetCellStringValue(row, 1),
                        DatasetAlias = GetCellStringValue(row, 2),
                        Notes = GetCellStringValue(row, 3)
                    };

                    if (!string.IsNullOrEmpty(dataset.DatasetName))
                    {
                        datasets.Add(dataset);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"读取要素集表失败: {ex.Message}");
            }

            return datasets;
        }

        /// <summary>
        /// 读取字段表
        /// </summary>
        private List<FieldDefinition> ReadFieldsTable(DataSet dataSet, string sheetName)
        {
            var fields = new List<FieldDefinition>();

            try
            {
                DataTable sheet = null;
                foreach (DataTable table in dataSet.Tables)
                {
                    if (table.TableName == sheetName)
                    {
                        sheet = table;
                        break;
                    }
                }

                if (sheet == null)
                {
                    LogWarning($"Excel中未找到'{sheetName}'工作表");
                    return fields;
                }

                foreach (DataRow row in sheet.Rows)
                {
                    var field = new FieldDefinition
                    {
                        Index = GetCellIntValue(row, 0),
                        FieldAlias = GetCellStringValue(row, 1),
                        FieldCode = GetCellStringValue(row, 2),
                        FieldType = GetCellStringValue(row, 3),
                        FieldLength = GetCellNullableIntValue(row, 4),
                        DecimalPlaces = GetCellNullableIntValue(row, 5),
                        ValueRange = GetCellStringValue(row, 6),
                        Constraint = GetCellStringValue(row, 7)
                    };

                    if (!string.IsNullOrEmpty(field.FieldCode) && !string.IsNullOrEmpty(field.FieldType))
                    {
                        fields.Add(field);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"读取字段表'{sheetName}'失败: {ex.Message}");
            }

            return fields;
        }

        /// <summary>
        /// 转换几何类型（兼容中文）
        /// </summary>
        private string ConvertToGeometryType(string geometryType)
        {
            if (string.IsNullOrWhiteSpace(geometryType))
            {
                return "POLYGON";
            }

            var normalized = geometryType.Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "POINT":
                case "点":
                    return "POINT";
                case "POLYLINE":
                case "LINE":
                case "线":
                    return "POLYLINE";
                case "POLYGON":
                case "面":
                    return "POLYGON";
                case "MULTIPOINT":
                case "多点":
                    return "MULTIPOINT";
                default:
                    return "POLYGON";
            }
        }

        /// <summary>
        /// 转换字段类型（兼容中文），返回 null 表示SHP不支持
        /// </summary>
        private string ConvertToShapefileFieldType(string fieldType)
        {
            if (string.IsNullOrWhiteSpace(fieldType))
            {
                return "TEXT";
            }

            var normalized = fieldType.Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "TEXT":
                case "STRING":
                case "文本":
                    return "TEXT";
                case "SHORT":
                case "SMALLINTEGER":
                case "短整型":
                    return "SHORT";
                case "LONG":
                case "INTEGER":
                case "长整型":
                case "整型":
                    return "LONG";
                case "FLOAT":
                case "SINGLE":
                case "单精度":
                    return "FLOAT";
                case "DOUBLE":
                case "双精度":
                    return "DOUBLE";
                case "DATE":
                case "DATETIME":
                case "日期":
                case "日期时间":
                    return "DATE";
                case "BLOB":
                case "二进制":
                case "GUID":
                case "全局唯一标识":
                    return null;
                default:
                    return "TEXT";
            }
        }

        /// <summary>
        /// 获取GP错误信息
        /// </summary>
        private string GetGpErrorMessage(IGPResult result)
        {
            try
            {
                if (result?.Messages != null && result.Messages.Any())
                {
                    return string.Join("; ", result.Messages);
                }
            }
            catch
            {
                // ignored
            }

            return "未知错误";
        }

        /// <summary>
        /// 获取单元格字符串值
        /// </summary>
        private string GetCellStringValue(DataRow row, int columnIndex)
        {
            if (row == null || columnIndex >= row.Table.Columns.Count)
            {
                return string.Empty;
            }

            var value = row[columnIndex];
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            return value.ToString()?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 获取单元格整数值
        /// </summary>
        private int GetCellIntValue(DataRow row, int columnIndex)
        {
            if (row == null || columnIndex >= row.Table.Columns.Count)
            {
                return 0;
            }

            var value = row[columnIndex];
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            if (value is double d)
            {
                return (int)d;
            }

            if (value is int i)
            {
                return i;
            }

            if (value is long l)
            {
                return (int)l;
            }

            int.TryParse(value.ToString(), out int result);
            return result;
        }

        /// <summary>
        /// 获取单元格可空整数值
        /// </summary>
        private int? GetCellNullableIntValue(DataRow row, int columnIndex)
        {
            if (row == null || columnIndex >= row.Table.Columns.Count)
            {
                return null;
            }

            var value = row[columnIndex];
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            if (value is double d)
            {
                return (int)d;
            }

            if (value is int i)
            {
                return i;
            }

            if (value is long l)
            {
                return (int)l;
            }

            if (int.TryParse(value.ToString(), out int result))
            {
                return result;
            }

            return null;
        }
    }
}
