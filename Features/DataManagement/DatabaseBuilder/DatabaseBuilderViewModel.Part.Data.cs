using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;
using ExcelDataReader;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.DatabaseBuilder
{
    public partial class DatabaseBuilderViewModel
    {

        /// <summary>
        /// 转换字符串为FieldType
        /// </summary>
        private FieldType ConvertToFieldType(string fieldType)
        {
            if (string.IsNullOrEmpty(fieldType)) return FieldType.String;

            var normalized = fieldType.Trim().ToUpperInvariant();

            switch (normalized)
            {
                case "TEXT":
                case "STRING":
                case "文本":
                    return FieldType.String;
                case "SHORT":
                case "SMALLINTEGER":
                case "短整型":
                    return FieldType.SmallInteger;
                case "LONG":
                case "INTEGER":
                case "长整型":
                case "整型":
                    return FieldType.Integer;
                case "FLOAT":
                case "SINGLE":
                case "单精度":
                    return FieldType.Single;
                case "DOUBLE":
                case "双精度":
                    return FieldType.Double;
                case "DATE":
                case "DATETIME":
                case "日期":
                case "日期时间":
                    return FieldType.Date;
                case "BLOB":
                case "二进制":
                    return FieldType.Blob;
                case "GUID":
                case "全局唯一标识":
                    return FieldType.GUID;
                default:
                    return FieldType.String;
            }
        }

        /// <summary>
        /// 转换字符串为GeometryType
        /// </summary>
        private GeometryType ConvertToGeometryType(string geometryType)
        {
            if (string.IsNullOrEmpty(geometryType)) return GeometryType.Polygon;

            var normalized = geometryType.Trim().ToUpperInvariant();

            switch (normalized)
            {
                case "POINT":
                case "点":
                    return GeometryType.Point;
                case "POLYLINE":
                case "LINE":
                case "线":
                    return GeometryType.Polyline;
                case "POLYGON":
                case "面":
                    return GeometryType.Polygon;
                case "MULTIPOINT":
                case "多点":
                    return GeometryType.Multipoint;
                default:
                    return GeometryType.Polygon;
            }
        }

        /// <summary>
        /// 读取Excel文件为DataSet
        /// </summary>
        private DataSet ReadExcelToDataSet(string excelPath)
        {
            try
            {
                // 注册编码提供程序以支持旧版Excel文件
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
                        var config = new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = _ => new ExcelDataTableConfiguration()
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
        private List<LayerDefinition> ReadLayersTable(string excelPath)
        {
            var layers = new List<LayerDefinition>();

            try
            {
                var dataSet = ReadExcelToDataSet(excelPath);
                if (dataSet == null) return null;

                // 查找"图层"工作表
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

                // 遍历数据行
                // 图层表列：序号、图层别名、几何类型、属性表名、要素集、约束、备注
                foreach (DataRow row in sheet.Rows)
                {
                    var layer = new LayerDefinition
                    {
                        Index = GetCellIntValue(row, 0),
                        LayerAlias = GetCellStringValue(row, 1),
                        GeometryType = GetCellStringValue(row, 2),
                        AttributesTable = GetCellStringValue(row, 3),
                        FeatureDataset = GetCellStringValue(row, 4),  // 要素集名称
                        Constraint = GetCellStringValue(row, 5),
                        Notes = GetCellStringValue(row, 6)
                    };

                    // 验证必要字段
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
        private List<FeatureDatasetInfo> ReadFeatureDatasetsTable(string excelPath)
        {
            var datasets = new List<FeatureDatasetInfo>();

            try
            {
                var dataSet = ReadExcelToDataSet(excelPath);
                if (dataSet == null) return datasets;

                // 查找"要素集"工作表
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
                    // 要素集表是可选的
                    return datasets;
                }

                // 遍历数据行
                // 要素集表列：序号、要素集名称、要素集别名、备注
                foreach (DataRow row in sheet.Rows)
                {
                    var dataset = new FeatureDatasetInfo
                    {
                        Index = GetCellIntValue(row, 0),
                        DatasetName = GetCellStringValue(row, 1),
                        DatasetAlias = GetCellStringValue(row, 2),
                        Notes = GetCellStringValue(row, 3)
                    };

                    // 验证必要字段
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
        private List<FieldDefinition> ReadFieldsTable(string excelPath, string sheetName)
        {
            var fields = new List<FieldDefinition>();

            try
            {
                var dataSet = ReadExcelToDataSet(excelPath);
                if (dataSet == null) return fields;

                // 查找指定工作表
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

                // 遍历数据行
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

                    // 验证必要字段
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
        /// 获取单元格字符串值
        /// </summary>
        private string GetCellStringValue(DataRow row, int columnIndex)
        {
            if (row == null || columnIndex >= row.Table.Columns.Count) return "";

            var value = row[columnIndex];
            if (value == null || value == DBNull.Value) return "";

            return value.ToString()?.Trim() ?? "";
        }

        /// <summary>
        /// 获取单元格整数值
        /// </summary>
        private int GetCellIntValue(DataRow row, int columnIndex)
        {
            if (row == null || columnIndex >= row.Table.Columns.Count) return 0;

            var value = row[columnIndex];
            if (value == null || value == DBNull.Value) return 0;

            if (value is double d) return (int)d;
            if (value is int i) return i;
            if (value is long l) return (int)l;

            int.TryParse(value.ToString(), out int result);
            return result;
        }

        /// <summary>
        /// 获取单元格可空整数值
        /// </summary>
        private int? GetCellNullableIntValue(DataRow row, int columnIndex)
        {
            if (row == null || columnIndex >= row.Table.Columns.Count) return null;

            var value = row[columnIndex];
            if (value == null || value == DBNull.Value) return null;

            if (value is double d) return (int)d;
            if (value is int i) return i;
            if (value is long l) return (int)l;

            if (int.TryParse(value.ToString(), out int result))
                return result;

            return null;
        }
    }
}
