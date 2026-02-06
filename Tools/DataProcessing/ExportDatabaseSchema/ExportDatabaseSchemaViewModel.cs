using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Tools.DataProcessing.ExportDatabaseSchema
{
    /// <summary>
    /// 要素集信息
    /// </summary>
    public class FeatureDatasetInfo
    {
        public int Index { get; set; }
        public string DatasetName { get; set; }
        public string DatasetAlias { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// 要素类信息
    /// </summary>
    public class FeatureClassInfo
    {
        public string Name { get; set; }
        public string AliasName { get; set; }
        public string GeometryType { get; set; }
        public string FeatureDataset { get; set; }
        public List<FieldInfo> Fields { get; set; } = new List<FieldInfo>();
    }

    /// <summary>
    /// 字段信息
    /// </summary>
    public class FieldInfo
    {
        public string FieldName { get; set; }
        public string AliasName { get; set; }
        public string FieldType { get; set; }
        public int? Length { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public bool IsNullable { get; set; }
        public string DefaultValue { get; set; }
    }

    /// <summary>
    /// 输出数据库属性结构表视图模型
    /// </summary>
    public class ExportDatabaseSchemaViewModel : PropertyChangedBase
    {
        #region 私有字段

        private string _inputGdbPath;
        private string _outputExcelPath;
        private bool _isProcessing;
        private string _logText;
        private CancellationTokenSource _cancellationTokenSource;

        #endregion

        #region 公共属性

        /// <summary>
        /// 输入GDB路径
        /// </summary>
        public string InputGdbPath
        {
            get => _inputGdbPath;
            set => SetProperty(ref _inputGdbPath, value);
        }

        /// <summary>
        /// 输出Excel路径
        /// </summary>
        public string OutputExcelPath
        {
            get => _outputExcelPath;
            set => SetProperty(ref _outputExcelPath, value);
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

        #endregion

        #region 命令

        public ICommand BrowseInputGdbCommand { get; private set; }
        public ICommand BrowseOutputExcelCommand { get; private set; }
        public ICommand StartCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }

        #endregion

        #region 构造函数

        public ExportDatabaseSchemaViewModel()
        {
            Initialize();
            InitializeCommands();
        }

        #endregion

        #region 私有方法

        private void Initialize()
        {
            _inputGdbPath = "";
            _outputExcelPath = "";
            _isProcessing = false;
            _logText = "";
        }

        private void InitializeCommands()
        {
            BrowseInputGdbCommand = new RelayCommand(() => BrowseInputGdb(), () => !IsProcessing);
            BrowseOutputExcelCommand = new RelayCommand(() => BrowseOutputExcel(), () => !IsProcessing);
            StartCommand = new RelayCommand(() => StartExport(), () => CanStart());
            StopCommand = new RelayCommand(() => StopExport(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
        }

        private void BrowseInputGdb()
        {
            var openDialog = new OpenItemDialog
            {
                Title = "选择文件地理数据库",
                MultiSelect = false,
                Filter = ItemFilters.Geodatabases
            };

            if (openDialog.ShowDialog() == true && openDialog.Items.Any())
            {
                InputGdbPath = openDialog.Items.First().Path;
                LogInfo($"已选择数据库: {InputGdbPath}");

                // 自动设置输出路径
                if (string.IsNullOrEmpty(OutputExcelPath))
                {
                    var gdbName = Path.GetFileNameWithoutExtension(InputGdbPath);
                    var parentDir = Path.GetDirectoryName(InputGdbPath);
                    OutputExcelPath = Path.Combine(parentDir, $"{gdbName}_属性结构表.xlsx");
                }
            }
        }

        private void BrowseOutputExcel()
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "选择Excel文件保存位置",
                Filter = "Excel文件 (*.xlsx)|*.xlsx|Excel 97-2003文件 (*.xls)|*.xls",
                DefaultExt = ".xlsx"
            };

            if (!string.IsNullOrEmpty(InputGdbPath))
            {
                var gdbName = Path.GetFileNameWithoutExtension(InputGdbPath);
                saveDialog.FileName = $"{gdbName}_属性结构表.xlsx";
                saveDialog.InitialDirectory = Path.GetDirectoryName(InputGdbPath);
            }

            if (saveDialog.ShowDialog() == true)
            {
                OutputExcelPath = saveDialog.FileName;
                LogInfo($"已选择输出路径: {OutputExcelPath}");
            }
        }

        private bool CanStart()
        {
            return !IsProcessing &&
                   !string.IsNullOrWhiteSpace(InputGdbPath) &&
                   !string.IsNullOrWhiteSpace(OutputExcelPath);
        }

        private async void StartExport()
        {
            IsProcessing = true;
            LogText = "";
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            var startTime = DateTime.Now;
            LogInfo($"开始时间: {startTime:yyyy年MM月dd日 HH:mm:ss}");

            try
            {
                // 验证输入
                if (!Directory.Exists(InputGdbPath))
                {
                    LogError("指定的数据库不存在。");
                    return;
                }

                // 读取数据库结构
                LogInfo("正在读取数据库结构...");
                var featureClasses = new List<FeatureClassInfo>();
                var featureDatasets = new List<FeatureDatasetInfo>();

                await QueuedTask.Run(() =>
                {
                    using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(InputGdbPath))))
                    {
                        // 获取所有要素数据集
                        var featureDatasetDefs = geodatabase.GetDefinitions<FeatureDatasetDefinition>();
                        var processedFCs = new HashSet<string>();

                        // 读取要素集信息
                        foreach (var datasetDef in featureDatasetDefs)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested) return;

                            var datasetName = datasetDef.GetName();
                            
                            featureDatasets.Add(new FeatureDatasetInfo
                            {
                                Index = featureDatasets.Count + 1,
                                DatasetName = datasetName,
                                DatasetAlias = datasetName, // 要素集没有别名属性，使用名称
                                Notes = ""
                            });
                            
                            LogInfo($"正在处理要素集: {datasetName}");
                        }

                        // 处理要素数据集中的要素类
                        foreach (var datasetDef in featureDatasetDefs)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested) return;

                            var datasetName = datasetDef.GetName();

                            using (var featureDataset = geodatabase.OpenDataset<FeatureDataset>(datasetName))
                            {
                                var fcDefs = featureDataset.GetDefinitions<FeatureClassDefinition>();
                                foreach (var fcDef in fcDefs)
                                {
                                    if (_cancellationTokenSource.Token.IsCancellationRequested) return;

                                    var fcInfo = ReadFeatureClassInfo(fcDef, datasetName);
                                    featureClasses.Add(fcInfo);
                                    processedFCs.Add(fcDef.GetName());
                                    LogInfo($"  读取要素类: {fcInfo.Name}");
                                }
                            }
                        }

                        // 处理根目录下的要素类
                        var allFcDefs = geodatabase.GetDefinitions<FeatureClassDefinition>();
                        foreach (var fcDef in allFcDefs)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested) return;

                            var fcName = fcDef.GetName();
                            if (!processedFCs.Contains(fcName))
                            {
                                var fcInfo = ReadFeatureClassInfo(fcDef, null);
                                featureClasses.Add(fcInfo);
                                LogInfo($"读取要素类: {fcInfo.Name}");
                            }
                        }

                        // 处理独立表
                        var tableDefs = geodatabase.GetDefinitions<TableDefinition>();
                        foreach (var tableDef in tableDefs)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested) return;

                            // 排除已处理的要素类（要素类也是表）
                            var tableName = tableDef.GetName();
                            if (!processedFCs.Contains(tableName) && !featureClasses.Any(fc => fc.Name == tableName))
                            {
                                var tableInfo = ReadTableInfo(tableDef);
                                featureClasses.Add(tableInfo);
                                LogInfo($"读取独立表: {tableInfo.Name}");
                            }
                        }
                    }
                });

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    LogWarning("操作已被用户取消");
                    return;
                }

                LogInfo($"共读取 {featureDatasets.Count} 个要素集，{featureClasses.Count} 个数据集");

                // 导出到Excel（在后台线程执行）
                LogInfo("正在导出到Excel...");
                await Task.Run(() => ExportToExcel(featureDatasets, featureClasses, OutputExcelPath));

                LogInfo($"导出完成！文件保存至: {OutputExcelPath}");
            }
            catch (Exception ex)
            {
                LogError($"导出过程中出错: {ex.Message}");
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

        private bool IsSystemField(string fieldName)
        {
            var systemFields = new[] { "OBJECTID", "Shape", "Shape_Length", "Shape_Area", "GlobalID" };
            return systemFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
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

        private void ExportToExcel(List<FeatureDatasetInfo> featureDatasets, List<FeatureClassInfo> featureClasses, string outputPath)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            var worksheetsToRelease = new List<Excel.Worksheet>();

            try
            {
                excelApp = new Excel.Application();
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;

                workbook = excelApp.Workbooks.Add();

                // 删除默认的工作表（保留一个）
                Excel.Sheets defaultSheets = workbook.Sheets;
                try
                {
                    while (defaultSheets.Count > 1)
                    {
                        Excel.Worksheet tempSheet = (Excel.Worksheet)defaultSheets[defaultSheets.Count];
                        try
                        {
                            tempSheet.Delete();
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(tempSheet);
                        }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(defaultSheets);
                }

                // 创建要素集表（如果有要素集）
                var usedNames = new HashSet<string>();
                if (featureDatasets.Count > 0)
                {
                    Excel.Sheets sheets1 = workbook.Sheets;
                    Excel.Worksheet datasetSheet = null;
                    try
                    {
                        datasetSheet = (Excel.Worksheet)sheets1[1];
                        datasetSheet.Name = "要素集";
                        CreateFeatureDatasetSheet(datasetSheet, featureDatasets);
                        usedNames.Add("要素集");
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sheets1);
                    }
                    if (datasetSheet != null) worksheetsToRelease.Add(datasetSheet);
                    
                    // 添加图层表
                    Excel.Sheets sheets2 = workbook.Sheets;
                    Excel.Worksheet summarySheet = null;
                    try
                    {
                        Excel.Worksheet afterSheet = (Excel.Worksheet)sheets2[sheets2.Count];
                        try
                        {
                            summarySheet = (Excel.Worksheet)sheets2.Add(After: afterSheet);
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(afterSheet);
                        }
                        summarySheet.Name = "图层";
                        CreateSummarySheet(summarySheet, featureClasses);
                        usedNames.Add("图层");
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sheets2);
                    }
                    if (summarySheet != null) worksheetsToRelease.Add(summarySheet);
                }
                else
                {
                    // 没有要素集，直接创建图层表
                    Excel.Sheets sheets1 = workbook.Sheets;
                    Excel.Worksheet summarySheet = null;
                    try
                    {
                        summarySheet = (Excel.Worksheet)sheets1[1];
                        summarySheet.Name = "图层";
                        CreateSummarySheet(summarySheet, featureClasses);
                        usedNames.Add("图层");
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(sheets1);
                    }
                    if (summarySheet != null) worksheetsToRelease.Add(summarySheet);
                }

                // 为每个要素类创建字段表
                foreach (var fc in featureClasses)
                {
                    if (fc.Fields.Count > 0)
                    {
                        // 工作表名称最多31个字符，且不能包含特殊字符
                        var sheetName = SanitizeSheetName(fc.Name);
                        if (sheetName.Length > 31) sheetName = sheetName.Substring(0, 31);

                        // 确保工作表名称唯一
                        var originalName = sheetName;
                        var counter = 1;
                        while (usedNames.Contains(sheetName))
                        {
                            var suffix = $"_{counter}";
                            sheetName = originalName.Length + suffix.Length > 31
                                ? originalName.Substring(0, 31 - suffix.Length) + suffix
                                : originalName + suffix;
                            counter++;
                        }
                        usedNames.Add(sheetName);

                        Excel.Sheets sheetsForField = workbook.Sheets;
                        Excel.Worksheet fieldSheet = null;
                        try
                        {
                            Excel.Worksheet afterSheet = (Excel.Worksheet)sheetsForField[sheetsForField.Count];
                            try
                            {
                                fieldSheet = (Excel.Worksheet)sheetsForField.Add(After: afterSheet);
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(afterSheet);
                            }
                            fieldSheet.Name = sheetName;
                            CreateFieldSheet(fieldSheet, fc);
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(sheetsForField);
                        }
                        if (fieldSheet != null) worksheetsToRelease.Add(fieldSheet);
                    }
                }

                // 保存文件
                if (outputPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    workbook.SaveAs(outputPath, Excel.XlFileFormat.xlOpenXMLWorkbook);
                }
                else
                {
                    workbook.SaveAs(outputPath, Excel.XlFileFormat.xlWorkbookNormal);
                }
            }
            finally
            {
                foreach (var ws in worksheetsToRelease)
                {
                    try { Marshal.ReleaseComObject(ws); } catch { }
                }
                try { if (workbook != null) { workbook.Close(false); Marshal.ReleaseComObject(workbook); } } catch { }
                try { if (excelApp != null) { excelApp.Quit(); Marshal.ReleaseComObject(excelApp); } } catch { }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private string SanitizeSheetName(string name)
        {
            // Excel工作表名称不能包含这些字符: \ / ? * [ ] :
            var invalidChars = new[] { '\\', '/', '?', '*', '[', ']', ':' };
            foreach (var c in invalidChars)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        private void CreateFeatureDatasetSheet(Excel.Worksheet sheet, List<FeatureDatasetInfo> featureDatasets)
        {
            // 表头
            var headers = new[] { "序号", "要素集名称", "要素集别名", "备注" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1] = headers[i];
            }

            // 设置表头样式
            Excel.Range headerRange = sheet.Range[sheet.Cells[1, 1], sheet.Cells[1, headers.Length]];
            try
            {
                headerRange.Font.Bold = true;
                headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                headerRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                headerRange.Borders.Weight = Excel.XlBorderWeight.xlThin;
            }
            finally
            {
                Marshal.ReleaseComObject(headerRange);
            }

            // 数据行
            for (int i = 0; i < featureDatasets.Count; i++)
            {
                var dataset = featureDatasets[i];
                var row = i + 2;
                sheet.Cells[row, 1] = dataset.Index;
                sheet.Cells[row, 2] = dataset.DatasetName;
                sheet.Cells[row, 3] = dataset.DatasetAlias ?? dataset.DatasetName;
                sheet.Cells[row, 4] = dataset.Notes;
            }

            // 设置数据区域边框
            if (featureDatasets.Count > 0)
            {
                Excel.Range dataRange = sheet.Range[sheet.Cells[2, 1], sheet.Cells[featureDatasets.Count + 1, headers.Length]];
                try
                {
                    dataRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                    dataRange.Borders.Weight = Excel.XlBorderWeight.xlThin;
                }
                finally
                {
                    Marshal.ReleaseComObject(dataRange);
                }
            }

            // 自动调整列宽
            sheet.Columns.AutoFit();
        }

        private void CreateSummarySheet(Excel.Worksheet sheet, List<FeatureClassInfo> featureClasses)
        {
            // 表头
            var headers = new[] { "序号", "图层别名", "几何类型", "属性表名", "要素集", "字段数", "备注" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1] = headers[i];
            }

            // 设置表头样式
            Excel.Range headerRange = sheet.Range[sheet.Cells[1, 1], sheet.Cells[1, headers.Length]];
            try
            {
                headerRange.Font.Bold = true;
                headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                headerRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                headerRange.Borders.Weight = Excel.XlBorderWeight.xlThin;
            }
            finally
            {
                Marshal.ReleaseComObject(headerRange);
            }

            // 数据行
            for (int i = 0; i < featureClasses.Count; i++)
            {
                var fc = featureClasses[i];
                var row = i + 2;
                sheet.Cells[row, 1] = i + 1;
                sheet.Cells[row, 2] = fc.AliasName ?? fc.Name;
                sheet.Cells[row, 3] = fc.GeometryType;
                sheet.Cells[row, 4] = fc.Name;
                sheet.Cells[row, 5] = fc.FeatureDataset;
                sheet.Cells[row, 6] = fc.Fields.Count;
                sheet.Cells[row, 7] = "";
            }

            // 设置数据区域边框
            if (featureClasses.Count > 0)
            {
                Excel.Range dataRange = sheet.Range[sheet.Cells[2, 1], sheet.Cells[featureClasses.Count + 1, headers.Length]];
                try
                {
                    dataRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                    dataRange.Borders.Weight = Excel.XlBorderWeight.xlThin;
                }
                finally
                {
                    Marshal.ReleaseComObject(dataRange);
                }
            }

            // 自动调整列宽
            sheet.Columns.AutoFit();
        }

        private void CreateFieldSheet(Excel.Worksheet sheet, FeatureClassInfo fc)
        {
            // 表头
            var headers = new[] { "序号", "字段别名", "字段代码", "字段类型", "字段长度", "精度", "小数位数", "允许空值", "默认值", "约束", "备注" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1] = headers[i];
            }

            // 设置表头样式
            Excel.Range headerRange = sheet.Range[sheet.Cells[1, 1], sheet.Cells[1, headers.Length]];
            try
            {
                headerRange.Font.Bold = true;
                headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                headerRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                headerRange.Borders.Weight = Excel.XlBorderWeight.xlThin;
            }
            finally
            {
                Marshal.ReleaseComObject(headerRange);
            }

            // 数据行
            for (int i = 0; i < fc.Fields.Count; i++)
            {
                var field = fc.Fields[i];
                var row = i + 2;
                sheet.Cells[row, 1] = i + 1;
                sheet.Cells[row, 2] = field.AliasName ?? field.FieldName;
                sheet.Cells[row, 3] = field.FieldName;
                sheet.Cells[row, 4] = field.FieldType;
                sheet.Cells[row, 5] = field.Length?.ToString() ?? "";
                sheet.Cells[row, 6] = field.Precision?.ToString() ?? "";
                sheet.Cells[row, 7] = field.Scale?.ToString() ?? "";
                sheet.Cells[row, 8] = field.IsNullable ? "是" : "否";
                sheet.Cells[row, 9] = field.DefaultValue;
                sheet.Cells[row, 10] = "";
                sheet.Cells[row, 11] = "";
            }

            // 设置数据区域边框
            if (fc.Fields.Count > 0)
            {
                Excel.Range dataRange = sheet.Range[sheet.Cells[2, 1], sheet.Cells[fc.Fields.Count + 1, headers.Length]];
                try
                {
                    dataRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                    dataRange.Borders.Weight = Excel.XlBorderWeight.xlThin;
                }
                finally
                {
                    Marshal.ReleaseComObject(dataRange);
                }
            }

            // 自动调整列宽
            sheet.Columns.AutoFit();
        }

        private void StopExport()
        {
            _cancellationTokenSource?.Cancel();
            LogWarning("正在停止操作...");
        }

        private void ShowHelp()
        {
            var helpText = "【输出数据库属性结构表】\n\n" +
                "功能说明：\n" +
                "将文件地理数据库(GDB)的属性结构导出为Excel文件，包含图层信息和字段定义。\n\n" +
                "参数说明：\n" +
                "- 输入数据库: 选择要导出结构的文件地理数据库(.gdb)\n" +
                "- 输出Excel: 选择导出的Excel文件路径(支持.xlsx和.xls格式)\n\n" +
                "输出内容：\n" +
                "- 图层汇总表: 包含所有要素类和表的基本信息\n" +
                "- 字段详情表: 每个要素类/表单独一个工作表，包含字段详细定义\n\n" +
                "使用步骤：\n" +
                "1. 点击\"浏览\"选择输入的GDB数据库\n" +
                "2. 点击\"浏览\"选择输出Excel文件路径\n" +
                "3. 点击\"开始导出\"执行导出操作\n\n" +
                "注意事项：\n" +
                "- 系统字段(OBJECTID、Shape等)不会被导出\n" +
                "- 工作表名称最长31个字符，超长会被截断\n" +
                "- 导出的Excel格式与建库模板兼容";

            MessageBox.Show(helpText, "帮助 - 输出数据库属性结构表", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        #endregion

        #region 日志方法

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}\n";
            
            // 确保在UI线程更新
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                LogText += logMessage;
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => LogText += logMessage);
            }
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 错误: {message}\n";
            
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                LogText += logMessage;
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => LogText += logMessage);
            }
        }

        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 警告: {message}\n";
            
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                LogText += logMessage;
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => LogText += logMessage);
            }
        }

        #endregion
    }
}
