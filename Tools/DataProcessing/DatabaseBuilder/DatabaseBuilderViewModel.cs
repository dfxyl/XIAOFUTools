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
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.DataProcessing.DatabaseBuilder
{
    /// <summary>
    /// 要素集信息类
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
        public string FeatureDataset { get; set; }  // 所属要素集（可为空）
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
    /// 属性表建库视图模型
    /// </summary>
    public class DatabaseBuilderViewModel : PropertyChangedBase
    {
        #region 私有字段

        private string _inputExcelPath;
        private string _outputFolderPath;
        private string _databaseName;
        private bool _isProcessing;
        private string _logText;
        private SpatialReference _selectedSpatialReference;
        private string _selectedCoordinateSystemName;
        private CancellationTokenSource _cancellationTokenSource;

        #endregion

        #region 公共属性

        /// <summary>
        /// 输入Excel文件路径
        /// </summary>
        public string InputExcelPath
        {
            get { return _inputExcelPath; }
            set
            {
                SetProperty(ref _inputExcelPath, value);
            }
        }

        /// <summary>
        /// 输出文件夹路径
        /// </summary>
        public string OutputFolderPath
        {
            get { return _outputFolderPath; }
            set
            {
                SetProperty(ref _outputFolderPath, value);
            }
        }

        /// <summary>
        /// 数据库名称
        /// </summary>
        public string DatabaseName
        {
            get { return _databaseName; }
            set
            {
                SetProperty(ref _databaseName, value);
            }
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get { return _isProcessing; }
            set
            {
                SetProperty(ref _isProcessing, value);
            }
        }

        /// <summary>
        /// 日志文本
        /// </summary>
        public string LogText
        {
            get { return _logText; }
            set
            {
                SetProperty(ref _logText, value);
            }
        }

        /// <summary>
        /// 选择的坐标系
        /// </summary>
        public SpatialReference SelectedSpatialReference
        {
            get { return _selectedSpatialReference; }
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
            get { return _selectedCoordinateSystemName; }
            set
            {
                SetProperty(ref _selectedCoordinateSystemName, value);
            }
        }

        #endregion

        #region 命令

        /// <summary>
        /// 浏览输入Excel命令
        /// </summary>
        public ICommand BrowseInputExcelCommand { get; private set; }

        /// <summary>
        /// 浏览输出文件夹命令
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

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public DatabaseBuilderViewModel()
        {
            Initialize();
            InitializeCommands();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化基本属性
        /// </summary>
        private void Initialize()
        {
            _inputExcelPath = "";
            _outputFolderPath = "";
            _databaseName = "NewDatabase";
            _isProcessing = false;
            _logText = "";
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
            StartCommand = new RelayCommand(() => StartBuildDatabase(), () => CanStart());
            StopCommand = new RelayCommand(() => StopBuildDatabase(), () => IsProcessing);
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
            // Excel文件不是GIS数据，使用Windows标准文件对话框
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
        /// 浏览输出文件夹
        /// </summary>
        private void BrowseOutputFolder()
        {
            // 使用 ArcGIS Pro 自带的文件夹浏览对话框
            var folderDialog = new OpenItemDialog
            {
                Title = "选择输出数据库位置",
                MultiSelect = false,
                Filter = ItemFilters.Folders
            };

            if (folderDialog.ShowDialog() == true && folderDialog.Items.Count() > 0)
            {
                OutputFolderPath = folderDialog.Items.First().Path;
                LogInfo($"已选择输出位置: {OutputFolderPath}");
            }
        }

        /// <summary>
        /// 导出Excel模板
        /// </summary>
        private void ExportTemplate()
        {
            try
            {
                // 获取模板文件路径
                string addinFolder = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string templatePath1 = System.IO.Path.Combine(addinFolder, "Data", "Excel模板", "建库模板.xls");
                string templatePath2 = System.IO.Path.Combine(addinFolder, "Data", "Excel模板", "建库模板-带要素集.xls");

                // 检查模板文件是否存在
                bool template1Exists = File.Exists(templatePath1);
                bool template2Exists = File.Exists(templatePath2);

                if (!template1Exists && !template2Exists)
                {
                    LogError("模板文件不存在");
                    MessageBox.Show("模板文件不存在", "错误");
                    return;
                }

                // 使用 ArcGIS Pro 自带的文件夹浏览对话框
                var folderDialog = new OpenItemDialog
                {
                    Title = "选择模板导出位置",
                    MultiSelect = false,
                    Filter = ItemFilters.Folders
                };

                if (folderDialog.ShowDialog() == true && folderDialog.Items.Count() > 0)
                {
                    string outputFolder = folderDialog.Items.First().Path;
                    int exportedCount = 0;

                    // 导出模板1
                    if (template1Exists)
                    {
                        string destPath1 = System.IO.Path.Combine(outputFolder, "建库模板.xls");
                        File.Copy(templatePath1, destPath1, true);
                        LogInfo($"模板已导出: 建库模板.xls");
                        exportedCount++;
                    }

                    // 导出模板2
                    if (template2Exists)
                    {
                        string destPath2 = System.IO.Path.Combine(outputFolder, "建库模板-带要素集.xls");
                        File.Copy(templatePath2, destPath2, true);
                        LogInfo($"模板已导出: 建库模板-带要素集.xls");
                        exportedCount++;
                    }

                    LogInfo($"共导出 {exportedCount} 个模板到: {outputFolder}");
                    MessageBox.Show($"已成功导出 {exportedCount} 个模板到:\n{outputFolder}", "导出成功");
                }
            }
            catch (Exception ex)
            {
                LogError($"导出模板失败: {ex.Message}");
                MessageBox.Show($"导出模板失败: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 判断是否可以开始
        /// </summary>
        private bool CanStart()
        {
            return !IsProcessing &&
                   !string.IsNullOrWhiteSpace(InputExcelPath) &&
                   !string.IsNullOrWhiteSpace(OutputFolderPath) &&
                   !string.IsNullOrWhiteSpace(DatabaseName) &&
                   SelectedSpatialReference != null;
        }

        /// <summary>
        /// 开始建库
        /// </summary>
        private async void StartBuildDatabase()
        {
            IsProcessing = true;
            LogText = "";
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            var startTime = DateTime.Now;
            LogInfo($"开始时间: {startTime:yyyy年MM月dd日 HH:mm:ss}");

            try
            {
                // 验证输入文件
                if (!File.Exists(InputExcelPath))
                {
                    LogError("指定的Excel文件不存在。");
                    return;
                }

                // 读取图层表
                LogInfo("正在读取Excel文件...");
                var layersTable = ReadLayersTable(InputExcelPath);
                if (layersTable == null || layersTable.Count == 0)
                {
                    LogError("无法读取图层表或图层表为空。");
                    return;
                }
                LogInfo($"读取到 {layersTable.Count} 个图层定义");

                // 读取要素集表
                var featureDatasets = ReadFeatureDatasetsTable(InputExcelPath);
                if (featureDatasets.Count > 0)
                {
                    LogInfo($"读取到 {featureDatasets.Count} 个要素集定义");
                }

                // 创建数据库
                string gdbPath = System.IO.Path.Combine(OutputFolderPath, DatabaseName + ".gdb");
                var targetSpatialReference = SelectedSpatialReference ?? SpatialReferences.WGS84;
                LogInfo($"目标坐标系: {targetSpatialReference.Name} (WKID: {targetSpatialReference.Wkid})");

                await QueuedTask.Run(() =>
                {
                    // 检查数据库是否存在，如果不存在则创建
                    if (!Directory.Exists(gdbPath))
                    {
                        LogInfo($"正在创建数据库: {DatabaseName}.gdb");
                        try
                        {
                            // 使用 Geoprocessing 创建文件地理数据库
                            var args = ArcGIS.Desktop.Core.Geoprocessing.Geoprocessing.MakeValueArray(OutputFolderPath, DatabaseName);
                            var result = ArcGIS.Desktop.Core.Geoprocessing.Geoprocessing.ExecuteToolAsync("CreateFileGDB_management", args, null, null, null, ArcGIS.Desktop.Core.Geoprocessing.GPExecuteToolFlags.None).Result;
                            if (result.IsFailed)
                            {
                                LogError("创建数据库失败");
                                return;
                            }
                            LogInfo($"数据库 {DatabaseName} 创建成功");
                        }
                        catch (Exception ex)
                        {
                            LogError($"创建数据库失败: {ex.Message}");
                            return;
                        }
                    }
                    else
                    {
                        LogInfo($"数据库 {DatabaseName} 已存在");
                    }

                    // 打开数据库
                    using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath))))
                    {
                        // 创建要素集（如果有定义）
                        var createdDatasets = new HashSet<string>();
                        foreach (var dataset in featureDatasets)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                return;
                            }

                            // 检查要素集是否存在
                            bool datasetExists = false;
                            try
                            {
                                var dsDef = geodatabase.GetDefinition<FeatureDatasetDefinition>(dataset.DatasetName);
                                datasetExists = dsDef != null;
                            }
                            catch
                            {
                                datasetExists = false;
                            }

                            if (!datasetExists)
                            {
                                try
                                {
                                    LogInfo($"正在创建要素集: {dataset.DatasetName}");
                                    
                                    // 使用 SchemaBuilder 创建要素集
                                    var datasetDescription = new FeatureDatasetDescription(dataset.DatasetName, targetSpatialReference);
                                    
                                    var schemaBuilder = new SchemaBuilder(geodatabase);
                                    schemaBuilder.Create(datasetDescription);
                                    
                                    if (schemaBuilder.Build())
                                    {
                                        LogInfo($"要素集 {dataset.DatasetName} 创建成功");
                                        createdDatasets.Add(dataset.DatasetName);
                                    }
                                    else
                                    {
                                        var errorInfo = schemaBuilder.ErrorMessages;
                                        var errorMsg = errorInfo != null && errorInfo.Count > 0 
                                            ? string.Join("; ", errorInfo) 
                                            : "未知错误";
                                        LogError($"创建要素集 {dataset.DatasetName} 失败: {errorMsg}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogError($"创建要素集 {dataset.DatasetName} 失败: {ex.Message}");
                                }
                            }
                            else
                            {
                                LogInfo($"要素集 {dataset.DatasetName} 已存在");
                                createdDatasets.Add(dataset.DatasetName);
                            }
                        }

                        // 遍历图层表创建要素类
                        foreach (var layer in layersTable)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                return;
                            }

                            string layerName = layer.AttributesTable;

                            // 检查要素类是否存在
                            bool featureClassExists = false;
                            try
                            {
                                var fcDef = geodatabase.GetDefinition<FeatureClassDefinition>(layerName);
                                featureClassExists = fcDef != null;
                            }
                            catch
                            {
                                featureClassExists = false;
                            }

                            if (!featureClassExists)
                            {
                                try
                                {
                                    // 读取字段表
                                    var fields = ReadFieldsTable(InputExcelPath, layer.AttributesTable);

                                    // 创建字段描述列表
                                    var fieldDescriptions = new List<FieldDescription>();
                                    
                                    if (fields != null && fields.Count > 0)
                                    {
                                        foreach (var field in fields)
                                        {
                                            if (string.IsNullOrEmpty(field.FieldCode))
                                            {
                                                continue;
                                            }

                                            // 转换字段类型
                                            var fieldType = ConvertToFieldType(field.FieldType);
                                            var fieldDesc = new FieldDescription(field.FieldCode, fieldType);
                                            
                                            // 设置别名
                                            if (!string.IsNullOrEmpty(field.FieldAlias))
                                            {
                                                fieldDesc.AliasName = field.FieldAlias;
                                            }

                                            // 设置字段长度（仅文本类型）
                                            if (fieldType == FieldType.String && field.FieldLength.HasValue && field.FieldLength.Value > 0)
                                            {
                                                fieldDesc.Length = field.FieldLength.Value;
                                            }
                                            else if (fieldType == FieldType.String)
                                            {
                                                fieldDesc.Length = 255; // 默认长度
                                            }

                                            fieldDescriptions.Add(fieldDesc);
                                        }
                                    }

                                    // 创建Shape字段描述
                                    var shapeType = ConvertToGeometryType(layer.GeometryType);
                                    var layerSpatialReference = targetSpatialReference;

                                    // 创建要素类描述
                                    FeatureClassDescription fcDescription;
                                    
                                    // 检查是否需要在要素集内创建
                                    bool hasFeatureDataset = !string.IsNullOrEmpty(layer.FeatureDataset) && createdDatasets.Contains(layer.FeatureDataset);

                                    FeatureDatasetDefinition datasetDef = null;
                                    if (hasFeatureDataset)
                                    {
                                        datasetDef = geodatabase.GetDefinition<FeatureDatasetDefinition>(layer.FeatureDataset);
                                        var datasetSpatialReference = datasetDef?.GetSpatialReference();
                                        if (datasetSpatialReference != null)
                                        {
                                            layerSpatialReference = datasetSpatialReference;
                                            if (datasetSpatialReference.Wkid > 0 && targetSpatialReference.Wkid > 0 && datasetSpatialReference.Wkid != targetSpatialReference.Wkid)
                                            {
                                                LogWarning($"要素集 {layer.FeatureDataset} 的坐标系与当前设置不一致，将使用要素集自身坐标系创建要素类 {layerName}");
                                            }
                                        }
                                    }

                                    var shapeDescription = new ShapeDescription(shapeType, layerSpatialReference);
                                    
                                    // 创建要素类描述
                                    fcDescription = new FeatureClassDescription(layerName, fieldDescriptions, shapeDescription);
                                    if (!string.IsNullOrEmpty(layer.LayerAlias))
                                    {
                                        fcDescription = new FeatureClassDescription(layerName, fieldDescriptions, shapeDescription)
                                        {
                                            AliasName = layer.LayerAlias
                                        };
                                    }

                                    // 使用 SchemaBuilder 创建要素类
                                    var schemaBuilder = new SchemaBuilder(geodatabase);
                                    
                                    if (hasFeatureDataset && datasetDef != null)
                                    {
                                        // 获取要素集的 Token
                                        var datasetToken = new FeatureDatasetDescription(datasetDef);
                                        
                                        // 在要素集内创建要素类
                                        LogInfo($"正在创建要素类: {layerName} (要素集: {layer.FeatureDataset})");
                                        schemaBuilder.Create(datasetToken, fcDescription);
                                    }
                                    else
                                    {
                                        if (hasFeatureDataset && datasetDef == null)
                                        {
                                            LogWarning($"未找到要素集 {layer.FeatureDataset}，将改为在数据库根目录创建要素类: {layerName}");
                                        }
                                        // 在数据库根目录创建要素类
                                        LogInfo($"正在创建要素类: {layerName}");
                                        schemaBuilder.Create(fcDescription);
                                    }
                                    
                                    if (schemaBuilder.Build())
                                    {
                                        LogInfo($"要素类 {layerName} 创建成功");
                                        if (!string.IsNullOrEmpty(layer.LayerAlias))
                                        {
                                            LogInfo($"要素类 {layerName} 别名设置为 {layer.LayerAlias}");
                                        }
                                        if (fieldDescriptions.Count > 0)
                                        {
                                            LogInfo($"已添加 {fieldDescriptions.Count} 个字段");
                                        }
                                    }
                                    else
                                    {
                                        var errorInfo = schemaBuilder.ErrorMessages;
                                        var errorMsg = errorInfo != null && errorInfo.Count > 0 
                                            ? string.Join("; ", errorInfo) 
                                            : "未知错误";
                                        LogError($"创建要素类 {layerName} 失败: {errorMsg}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogError($"创建要素类 {layerName} 失败: {ex.Message}");
                                }
                            }
                            else
                            {
                                LogInfo($"要素类 {layerName} 已存在");
                            }
                        }
                    }
                });

                LogInfo("建库完成！");
            }
            catch (Exception ex)
            {
                LogError($"建库过程中出错: {ex.Message}");
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
        /// 停止建库
        /// </summary>
        private void StopBuildDatabase()
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

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "属性表建库工具使用说明\n\n" +
                               "功能描述：\n" +
                               "根据Excel属性结构表自动创建文件地理数据库、要素集及要素类。\n\n" +
                               "参数说明：\n" +
                               "1. 属性结构表 (Excel)：包含图层和字段定义的Excel文件\n" +
                               "2. 输出数据库位置：文件地理数据库的保存位置\n" +
                               "3. 数据库名称：要创建的数据库名称（不含.gdb后缀）\n" +
                               "4. 图层坐标系：设置新建要素集和要素类的坐标系（默认WGS84）\n\n" +
                               "Excel模板格式：\n" +
                               "- 必须包含名为'图层'的工作表\n" +
                               "- 可选包含名为'要素集'的工作表\n" +
                               "- 要素集表列：序号、要素集名称、要素集别名、备注\n" +
                               "- 图层表列：序号、图层别名、几何类型、属性表名、要素集、约束、备注\n" +
                               "- 每个图层对应一个同名工作表定义字段\n" +
                               "- 字段表列：序号、字段别名、字段代码、字段类型、字段长度、小数位数、值域、约束\n\n" +
                               "几何类型：\n" +
                               "- POINT：点\n" +
                               "- POLYLINE：线\n" +
                               "- POLYGON：面\n" +
                               "- MULTIPOINT：多点\n\n" +
                               "字段类型：\n" +
                               "- TEXT：文本\n" +
                               "- SHORT：短整型\n" +
                               "- LONG：长整型\n" +
                               "- FLOAT：单精度浮点\n" +
                               "- DOUBLE：双精度浮点\n" +
                               "- DATE：日期\n\n" +
                               "操作步骤：\n" +
                               "1. 点击'导出Excel模板'获取模板文件\n" +
                               "2. 按模板格式填写要素集、图层和字段定义\n" +
                               "3. 选择填写好的Excel文件\n" +
                               "4. 选择输出位置并输入数据库名称\n" +
                               "5. 选择图层坐标系\n" +
                               "6. 点击'开始建库'执行创建";

            MessageBox.Show(helpContent, "属性表建库工具使用说明");
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

        #endregion
    }
}
