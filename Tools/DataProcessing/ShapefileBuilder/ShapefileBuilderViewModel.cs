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
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.DataProcessing.ShapefileBuilder
{
    /// <summary>
    /// 要素集信息类（SHP模式下仅用于兼容模板校验）
    /// </summary>
    public class FeatureDatasetInfo
    {
        public int Index { get; set; }
        public string DatasetName { get; set; }
        public string DatasetAlias { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// 图层信息类
    /// </summary>
    public class LayerDefinition
    {
        public int Index { get; set; }
        public string LayerAlias { get; set; }
        public string GeometryType { get; set; }
        public string AttributesTable { get; set; }
        public string FeatureDataset { get; set; }
        public string Constraint { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// 字段信息类
    /// </summary>
    public class FieldDefinition
    {
        public int Index { get; set; }
        public string FieldAlias { get; set; }
        public string FieldCode { get; set; }
        public string FieldType { get; set; }
        public int? FieldLength { get; set; }
        public int? DecimalPlaces { get; set; }
        public string ValueRange { get; set; }
        public string Constraint { get; set; }
    }

    /// <summary>
    /// 属性表建SHP视图模型
    /// </summary>
    public class ShapefileBuilderViewModel : PropertyChangedBase
    {
        private string _inputExcelPath;
        private string _outputFolderPath;
        private bool _isProcessing;
        private string _logText;
        private SpatialReference _selectedSpatialReference;
        private string _selectedCoordinateSystemName;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 输入Excel文件路径
        /// </summary>
        public string InputExcelPath
        {
            get => _inputExcelPath;
            set => SetProperty(ref _inputExcelPath, value);
        }

        /// <summary>
        /// 输出目录路径
        /// </summary>
        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set => SetProperty(ref _outputFolderPath, value);
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        /// <summary>
        /// 日志文本
        /// </summary>
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        /// <summary>
        /// 选择的坐标系
        /// </summary>
        public SpatialReference SelectedSpatialReference
        {
            get => _selectedSpatialReference;
            set
            {
                if (SetProperty(ref _selectedSpatialReference, value))
                {
                    SelectedCoordinateSystemName = value == null
                        ? "未选择坐标系"
                        : $"{value.Name} (WKID: {value.Wkid})";
                }
            }
        }

        /// <summary>
        /// 选择的坐标系名称
        /// </summary>
        public string SelectedCoordinateSystemName
        {
            get => _selectedCoordinateSystemName;
            set => SetProperty(ref _selectedCoordinateSystemName, value);
        }

        /// <summary>
        /// 浏览输入Excel命令
        /// </summary>
        public ICommand BrowseInputExcelCommand { get; private set; }

        /// <summary>
        /// 浏览输出目录命令
        /// </summary>
        public ICommand BrowseOutputFolderCommand { get; private set; }

        /// <summary>
        /// 导出模板命令
        /// </summary>
        public ICommand ExportTemplateCommand { get; private set; }

        /// <summary>
        /// 选择坐标系命令
        /// </summary>
        public ICommand SelectCoordinateSystemCommand { get; private set; }

        /// <summary>
        /// 开始执行命令
        /// </summary>
        public ICommand StartCommand { get; private set; }

        /// <summary>
        /// 停止执行命令
        /// </summary>
        public ICommand StopCommand { get; private set; }

        /// <summary>
        /// 帮助命令
        /// </summary>
        public ICommand ShowHelpCommand { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ShapefileBuilderViewModel()
        {
            Initialize();
            InitializeCommands();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void Initialize()
        {
            _inputExcelPath = string.Empty;
            _outputFolderPath = string.Empty;
            _isProcessing = false;
            _logText = string.Empty;
            _selectedSpatialReference = SpatialReferences.WGS84;
            _selectedCoordinateSystemName = $"{_selectedSpatialReference.Name} (WKID: {_selectedSpatialReference.Wkid})";
        }

        /// <summary>
        /// 初始化命令
        /// </summary>
        private void InitializeCommands()
        {
            BrowseInputExcelCommand = new RelayCommand(() => BrowseInputExcel(), () => !IsProcessing);
            BrowseOutputFolderCommand = new RelayCommand(() => BrowseOutputFolder(), () => !IsProcessing);
            ExportTemplateCommand = new RelayCommand(() => ExportTemplate(), () => !IsProcessing);
            SelectCoordinateSystemCommand = new RelayCommand(() => SelectCoordinateSystem(), () => !IsProcessing);
            StartCommand = new RelayCommand(() => StartBuildShapefile(), () => CanStart());
            StopCommand = new RelayCommand(() => StopBuildShapefile(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
        }

        /// <summary>
        /// 选择坐标系
        /// </summary>
        private void SelectCoordinateSystem()
        {
            try
            {
                var selectedSpatialRef = CoordinateSystemSelector.ShowCoordinateSystemDialog();
                if (selectedSpatialRef != null)
                {
                    SelectedSpatialReference = selectedSpatialRef;
                    LogInfo($"已选择图层坐标系: {SelectedCoordinateSystemName}");
                }
            }
            catch (Exception ex)
            {
                LogError($"选择坐标系失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 浏览输入Excel文件
        /// </summary>
        private void BrowseInputExcel()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "选择属性结构表Excel文件",
                Filter = "Excel文件 (*.xls;*.xlsx)|*.xls;*.xlsx|所有文件 (*.*)|*.*",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == true)
            {
                InputExcelPath = openFileDialog.FileName;
                LogInfo($"已选择输入文件: {InputExcelPath}");
            }
        }

        /// <summary>
        /// 浏览输出目录
        /// </summary>
        private void BrowseOutputFolder()
        {
            var folderDialog = new OpenItemDialog
            {
                Title = "选择输出SHP目录",
                MultiSelect = false,
                Filter = ItemFilters.Folders
            };

            if (folderDialog.ShowDialog() == true && folderDialog.Items.Count() > 0)
            {
                OutputFolderPath = folderDialog.Items.First().Path;
                LogInfo($"已选择输出目录: {OutputFolderPath}");
            }
        }

        /// <summary>
        /// 导出Excel模板
        /// </summary>
        private void ExportTemplate()
        {
            try
            {
                string addinFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string templatePath = Path.Combine(addinFolder, "Data", "Excel模板", "建SHP模板.xls");

                if (!File.Exists(templatePath))
                {
                    LogError("建SHP模板文件不存在: 建SHP模板.xls");
                    MessageBox.Show("建SHP模板文件不存在: 建SHP模板.xls", "错误");
                    return;
                }

                var folderDialog = new OpenItemDialog
                {
                    Title = "选择模板导出位置",
                    MultiSelect = false,
                    Filter = ItemFilters.Folders
                };

                if (folderDialog.ShowDialog() == true && folderDialog.Items.Count() > 0)
                {
                    string outputFolder = folderDialog.Items.First().Path;
                    string destPath = Path.Combine(outputFolder, "建SHP模板.xls");
                    File.Copy(templatePath, destPath, true);

                    LogInfo($"模板已导出: {destPath}");
                    MessageBox.Show($"已成功导出模板到:\n{destPath}", "导出成功");
                }
            }
            catch (Exception ex)
            {
                LogError($"导出模板失败: {ex.Message}");
                MessageBox.Show($"导出模板失败: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 判断是否可开始
        /// </summary>
        private bool CanStart()
        {
            return !IsProcessing
                   && !string.IsNullOrWhiteSpace(InputExcelPath)
                   && !string.IsNullOrWhiteSpace(OutputFolderPath)
                   && SelectedSpatialReference != null;
        }

        /// <summary>
        /// 开始建SHP
        /// </summary>
        private async void StartBuildShapefile()
        {
            IsProcessing = true;
            LogText = string.Empty;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            var startTime = DateTime.Now;
            LogInfo($"开始时间: {startTime:yyyy年MM月dd日 HH:mm:ss}");

            try
            {
                if (!File.Exists(InputExcelPath))
                {
                    LogError("指定的Excel文件不存在。");
                    return;
                }

                if (!Directory.Exists(OutputFolderPath))
                {
                    Directory.CreateDirectory(OutputFolderPath);
                    LogInfo($"输出目录不存在，已自动创建: {OutputFolderPath}");
                }

                LogInfo("正在读取Excel文件...");
                var dataSet = ReadExcelToDataSet(InputExcelPath);
                if (dataSet == null)
                {
                    LogError("读取Excel文件失败。");
                    return;
                }

                var layers = ReadLayersTable(dataSet);
                if (layers == null || layers.Count == 0)
                {
                    LogError("无法读取图层表或图层表为空。");
                    return;
                }

                LogInfo($"读取到 {layers.Count} 个图层定义");

                var featureDatasets = ReadFeatureDatasetsTable(dataSet);
                if (featureDatasets.Count > 0)
                {
                    LogWarning($"检测到 {featureDatasets.Count} 个要素集定义，SHP不支持要素集，已自动忽略。");
                }

                var targetSpatialReference = SelectedSpatialReference ?? SpatialReferences.WGS84;
                LogInfo($"目标坐标系: {targetSpatialReference.Name} (WKID: {targetSpatialReference.Wkid})");

                int createdCount = 0;
                int skippedCount = 0;
                int failedCount = 0;

                foreach (var layer in layers)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        LogWarning("操作已被用户取消");
                        break;
                    }

                    string originalName = layer.AttributesTable;
                    string shpName = NormalizeShapefileDatasetName(originalName, layer.Index);
                    if (!string.Equals(originalName, shpName, StringComparison.OrdinalIgnoreCase))
                    {
                        LogWarning($"图层名 '{originalName}' 不符合SHP命名要求，已规范为 '{shpName}'。");
                    }

                    string shpPath = Path.Combine(OutputFolderPath, shpName + ".shp");
                    if (File.Exists(shpPath))
                    {
                        LogWarning($"SHP已存在，跳过: {shpName}.shp");
                        skippedCount++;
                        continue;
                    }

                    try
                    {
                        string geometryType = ConvertToGeometryType(layer.GeometryType);
                        var createParams = Geoprocessing.MakeValueArray(
                            OutputFolderPath,
                            shpName + ".shp",
                            geometryType,
                            null,
                            "DISABLED",
                            "DISABLED",
                            targetSpatialReference);

                        LogInfo($"正在创建SHP: {shpName}.shp ({geometryType})");
                        var createResult = await Geoprocessing.ExecuteToolAsync(
                            "CreateFeatureclass_management",
                            createParams,
                            null,
                            _cancellationTokenSource.Token);

                        if (createResult.IsFailed)
                        {
                            failedCount++;
                            LogError($"创建SHP失败: {shpName}.shp，{GetGpErrorMessage(createResult)}");
                            continue;
                        }

                        var fields = ReadFieldsTable(dataSet, originalName);
                        var usedFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "FID",
                            "OID",
                            "OBJECTID",
                            "SHAPE",
                            "SHAPE_LEN",
                            "SHAPE_LENG",
                            "SHAPE_AREA"
                        };

                        int addFieldCount = 0;
                        foreach (var field in fields)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                break;
                            }

                            if (string.IsNullOrWhiteSpace(field.FieldCode))
                            {
                                continue;
                            }

                            string shpFieldType = ConvertToShapefileFieldType(field.FieldType);
                            if (string.IsNullOrWhiteSpace(shpFieldType))
                            {
                                LogWarning($"字段 {field.FieldCode} 类型 {field.FieldType} 不支持SHP，已跳过。");
                                continue;
                            }

                            string normalizedFieldName = NormalizeShapefileFieldName(field.FieldCode, usedFieldNames);
                            if (!string.Equals(field.FieldCode, normalizedFieldName, StringComparison.OrdinalIgnoreCase))
                            {
                                LogWarning($"字段名 '{field.FieldCode}' 已规范为 '{normalizedFieldName}'。");
                            }

                            object precision = null;
                            object scale = null;
                            object length = null;

                            if (string.Equals(shpFieldType, "TEXT", StringComparison.OrdinalIgnoreCase))
                            {
                                length = NormalizeTextLength(field.FieldLength);
                            }
                            else if ((string.Equals(shpFieldType, "FLOAT", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(shpFieldType, "DOUBLE", StringComparison.OrdinalIgnoreCase)))
                            {
                                if (field.FieldLength.HasValue && field.FieldLength.Value > 0)
                                {
                                    precision = field.FieldLength.Value;
                                }

                                if (field.DecimalPlaces.HasValue && field.DecimalPlaces.Value >= 0)
                                {
                                    scale = field.DecimalPlaces.Value;
                                }
                            }

                            var addFieldParams = Geoprocessing.MakeValueArray(
                                shpPath,
                                normalizedFieldName,
                                shpFieldType,
                                precision,
                                scale,
                                length,
                                field.FieldAlias);

                            var addFieldResult = await Geoprocessing.ExecuteToolAsync(
                                "AddField_management",
                                addFieldParams,
                                null,
                                _cancellationTokenSource.Token);

                            if (addFieldResult.IsFailed)
                            {
                                LogError($"字段创建失败: {shpName}.{normalizedFieldName}，{GetGpErrorMessage(addFieldResult)}");
                                continue;
                            }

                            addFieldCount++;
                        }

                        createdCount++;
                        LogInfo($"SHP创建成功: {shpName}.shp，已添加 {addFieldCount} 个字段");
                    }
                    catch (OperationCanceledException)
                    {
                        LogWarning("操作已被用户取消");
                        break;
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        LogError($"创建SHP失败: {shpName}.shp，{ex.Message}");
                    }
                }

                LogInfo($"建SHP完成：成功 {createdCount}，跳过 {skippedCount}，失败 {failedCount}");
            }
            catch (Exception ex)
            {
                LogError($"建SHP过程中出错: {ex.Message}");
            }
            finally
            {
                var endTime = DateTime.Now;
                LogInfo($"结束时间: {endTime:yyyy年MM月dd日 HH:mm:ss}");
                LogInfo($"历时: {endTime - startTime}");
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 停止建SHP
        /// </summary>
        private void StopBuildShapefile()
        {
            _cancellationTokenSource?.Cancel();
            LogWarning("正在停止操作...");
        }

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
        /// 规范化SHP要素类名称
        /// </summary>
        private string NormalizeShapefileDatasetName(string layerName, int index)
        {
            string name = (layerName ?? string.Empty).Trim();
            if (name.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidChar, '_');
            }

            name = name.Replace(' ', '_');

            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Layer_{Math.Max(1, index)}";
            }

            if (char.IsDigit(name[0]))
            {
                name = "L" + name;
            }

            if (name.Length > 64)
            {
                name = name.Substring(0, 64);
            }

            return name;
        }

        /// <summary>
        /// 规范化SHP字段名（长度限制10，并保证唯一）
        /// </summary>
        private string NormalizeShapefileFieldName(string fieldName, HashSet<string> usedFieldNames)
        {
            string name = (fieldName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "FIELD";
            }

            name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "FIELD";
            }

            if (char.IsDigit(name[0]))
            {
                name = "F" + name;
            }

            if (name.Length > 10)
            {
                name = name.Substring(0, 10);
            }

            string candidate = name;
            int suffix = 1;
            while (usedFieldNames.Contains(candidate))
            {
                string suffixText = suffix.ToString();
                int baseLength = Math.Max(1, 10 - suffixText.Length);
                string baseName = name.Length > baseLength ? name.Substring(0, baseLength) : name;
                candidate = baseName + suffixText;
                suffix++;
            }

            usedFieldNames.Add(candidate);
            return candidate;
        }

        /// <summary>
        /// 规范化文本字段长度
        /// </summary>
        private int NormalizeTextLength(int? length)
        {
            int value = length.GetValueOrDefault(254);
            if (value <= 0)
            {
                value = 254;
            }

            if (value > 254)
            {
                value = 254;
            }

            return value;
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

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "属性表建SHP工具使用说明\n\n"
                               + "功能描述：\n"
                               + "根据Excel属性结构表自动批量创建Shapefile图层。\n\n"
                               + "参数说明：\n"
                               + "1. 属性结构表 (Excel)：包含图层和字段定义的Excel文件\n"
                               + "2. 输出SHP目录：Shapefile输出文件夹\n"
                               + "3. 图层坐标系：设置新建SHP图层坐标系（默认WGS84）\n\n"
                               + "Excel模板格式：\n"
                               + "- 必须包含名为'图层'的工作表\n"
                               + "- 可选包含名为'要素集'的工作表（SHP模式会忽略）\n"
                               + "- 图层表列：序号、图层别名、几何类型、属性表名、要素集、约束、备注\n"
                               + "- 每个图层对应一个同名工作表定义字段\n"
                               + "- 字段表列：序号、字段别名、字段代码、字段类型、字段长度、小数位数、值域、约束\n\n"
                               + "几何类型：\n"
                               + "- POINT：点\n"
                               + "- POLYLINE：线\n"
                               + "- POLYGON：面\n"
                               + "- MULTIPOINT：多点\n\n"
                               + "字段类型（SHP支持）：\n"
                               + "- TEXT：文本\n"
                               + "- SHORT：短整型\n"
                               + "- LONG：长整型\n"
                               + "- FLOAT：单精度浮点\n"
                               + "- DOUBLE：双精度浮点\n"
                               + "- DATE：日期\n\n"
                               + "注意事项：\n"
                               + "- 模板文件名为 建SHP模板.xls\n"
                               + "- SHP字段名最大10字符，超长会自动截断并去重\n"
                               + "- SHP不支持BLOB、GUID等字段类型，将自动跳过\n"
                               + "- SHP不支持要素集结构\n\n"
                               + "操作步骤：\n"
                               + "1. 点击'导出模板'获取模板文件\n"
                               + "2. 按模板格式填写图层和字段定义\n"
                               + "3. 选择填写好的Excel文件\n"
                               + "4. 选择输出目录并设置坐标系\n"
                               + "5. 点击'开始建SHP'执行创建";

            MessageBox.Show(helpContent, "属性表建SHP工具使用说明");
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogText += $"[{timestamp}] {message}\n";
            });
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogText += $"[{timestamp}] 警告: {message}\n";
            });
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogText += $"[{timestamp}] 错误: {message}\n";
            });
        }
    }
}
