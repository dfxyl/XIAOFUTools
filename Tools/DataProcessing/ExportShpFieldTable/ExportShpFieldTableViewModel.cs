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

namespace XIAOFUTools.Tools.DataProcessing.ExportShpFieldTable
{
    /// <summary>
    /// SHP图层结构信息
    /// </summary>
    public class ShpLayerSchemaInfo
    {
        public string Name { get; set; }
        public string AliasName { get; set; }
        public string GeometryType { get; set; }
        public List<ShpFieldSchemaInfo> Fields { get; } = new List<ShpFieldSchemaInfo>();
    }

    /// <summary>
    /// SHP字段结构信息
    /// </summary>
    public class ShpFieldSchemaInfo
    {
        public string FieldName { get; set; }
        public string AliasName { get; set; }
        public string FieldType { get; set; }
        public int? Length { get; set; }
        public int? Scale { get; set; }
    }

    /// <summary>
    /// SHP输字段表视图模型
    /// </summary>
    public class ExportShpFieldTableViewModel : PropertyChangedBase
    {
        private string _inputFolderPath;
        private string _outputExcelPath;
        private bool _isProcessing;
        private string _logText;
        private CancellationTokenSource _cancellationTokenSource;

        public string InputFolderPath
        {
            get => _inputFolderPath;
            set => SetProperty(ref _inputFolderPath, value);
        }

        public string OutputExcelPath
        {
            get => _outputExcelPath;
            set => SetProperty(ref _outputExcelPath, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        public ICommand BrowseInputFolderCommand { get; private set; }
        public ICommand BrowseOutputExcelCommand { get; private set; }
        public ICommand StartCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }

        public ExportShpFieldTableViewModel()
        {
            _inputFolderPath = string.Empty;
            _outputExcelPath = string.Empty;
            _logText = string.Empty;

            BrowseInputFolderCommand = new RelayCommand(() => BrowseInputFolder(), () => !IsProcessing);
            BrowseOutputExcelCommand = new RelayCommand(() => BrowseOutputExcel(), () => !IsProcessing);
            StartCommand = new RelayCommand(() => StartExport(), () => CanStart());
            StopCommand = new RelayCommand(() => StopExport(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
        }

        private void BrowseInputFolder()
        {
            var folderDialog = new OpenItemDialog
            {
                Title = "选择输入SHP目录",
                MultiSelect = false,
                Filter = ItemFilters.Folders
            };

            if (folderDialog.ShowDialog() == true && folderDialog.Items.Any())
            {
                InputFolderPath = folderDialog.Items.First().Path;
                LogInfo($"已选择输入目录: {InputFolderPath}");

                if (string.IsNullOrWhiteSpace(OutputExcelPath))
                {
                    OutputExcelPath = Path.Combine(InputFolderPath, "SHP字段表.xlsx");
                }
            }
        }

        private void BrowseOutputExcel()
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "选择输出字段表",
                Filter = "Excel文件 (*.xlsx)|*.xlsx|Excel 97-2003文件 (*.xls)|*.xls",
                DefaultExt = ".xlsx",
                FileName = string.IsNullOrWhiteSpace(OutputExcelPath) ? "SHP字段表.xlsx" : Path.GetFileName(OutputExcelPath)
            };

            if (!string.IsNullOrWhiteSpace(InputFolderPath) && Directory.Exists(InputFolderPath))
            {
                saveDialog.InitialDirectory = InputFolderPath;
            }

            if (saveDialog.ShowDialog() == true)
            {
                OutputExcelPath = saveDialog.FileName;
                LogInfo($"已选择输出表: {OutputExcelPath}");
            }
        }

        private bool CanStart()
        {
            return !IsProcessing
                   && !string.IsNullOrWhiteSpace(InputFolderPath)
                   && !string.IsNullOrWhiteSpace(OutputExcelPath);
        }

        private async void StartExport()
        {
            IsProcessing = true;
            LogText = string.Empty;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            var startTime = DateTime.Now;
            LogInfo($"开始时间: {startTime:yyyy年MM月dd日 HH:mm:ss}");

            try
            {
                if (!Directory.Exists(InputFolderPath))
                {
                    LogError("输入目录不存在。");
                    return;
                }

                var shpFiles = Directory.GetFiles(InputFolderPath, "*.shp", SearchOption.TopDirectoryOnly);
                if (shpFiles.Length == 0)
                {
                    LogWarning("输入目录中未找到SHP文件。");
                    return;
                }

                var outputDir = Path.GetDirectoryName(OutputExcelPath);
                if (!string.IsNullOrWhiteSpace(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                var layerInfos = new List<ShpLayerSchemaInfo>();
                int readFailCount = 0;

                LogInfo("正在读取SHP字段结构...");

                await QueuedTask.Run(() =>
                {
                    var connectionPath = new FileSystemConnectionPath(new Uri(InputFolderPath), FileSystemDatastoreType.Shapefile);
                    using var datastore = new FileSystemDatastore(connectionPath);

                    foreach (var shpPath in shpFiles)
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            return;
                        }

                        var layerName = Path.GetFileNameWithoutExtension(shpPath);
                        try
                        {
                            using var featureClass = datastore.OpenDataset<FeatureClass>(layerName);
                            var definition = featureClass.GetDefinition();

                            var layerInfo = new ShpLayerSchemaInfo
                            {
                                Name = layerName,
                                AliasName = layerName,
                                GeometryType = ConvertGeometryType(definition.GetShapeType())
                            };

                            foreach (var field in definition.GetFields())
                            {
                                if (IsSystemField(field.Name))
                                {
                                    continue;
                                }

                                layerInfo.Fields.Add(new ShpFieldSchemaInfo
                                {
                                    FieldName = field.Name,
                                    AliasName = field.AliasName,
                                    FieldType = ConvertFieldType(field.FieldType),
                                    Length = field.FieldType == FieldType.String ? field.Length : null,
                                    Scale = field.Scale > 0 ? field.Scale : null
                                });
                            }

                            layerInfos.Add(layerInfo);
                            LogInfo($"读取完成: {layerName}，字段数 {layerInfo.Fields.Count}");
                        }
                        catch (Exception ex)
                        {
                            readFailCount++;
                            LogError($"读取失败: {layerName}，{ex.Message}");
                        }
                    }
                });

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    LogWarning("操作已被用户取消。");
                    return;
                }

                if (layerInfos.Count == 0)
                {
                    LogError("没有可导出的SHP结构。");
                    return;
                }

                LogInfo("正在写出字段表...");
                await RunExportOnStaThreadAsync(layerInfos, OutputExcelPath, _cancellationTokenSource.Token);

                LogInfo($"导出完成！成功读取 {layerInfos.Count} 个SHP，读取失败 {readFailCount} 个");
                LogInfo($"输出文件: {OutputExcelPath}");
            }
            catch (OperationCanceledException)
            {
                LogWarning("操作已被用户取消。");
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

        private void ExportToExcel(List<ShpLayerSchemaInfo> layers, string outputPath, CancellationToken token)
        {
            const int xlOpenXMLWorkbook = 51;
            const int xlWorkbookNormal = -4143;

            dynamic excelApp = null;
            dynamic workbook = null;
            var worksheetsToRelease = new List<object>();

            try
            {
                LogInfo("正在启动Excel组件...");
                var excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType == null)
                {
                    throw new Exception("未找到Excel组件，请确认已安装Office。 ");
                }

                excelApp = Activator.CreateInstance(excelType);
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;

                workbook = excelApp.Workbooks.Add();

                dynamic defaultSheets = workbook.Sheets;
                try
                {
                    while (defaultSheets.Count > 1)
                    {
                        dynamic tempSheet = defaultSheets[defaultSheets.Count];
                        try
                        {
                            tempSheet.Delete();
                        }
                        finally
                        {
                            ReleaseComObject(tempSheet);
                        }
                    }
                }
                finally
                {
                    ReleaseComObject(defaultSheets);
                }

                dynamic summarySheets = workbook.Sheets;
                dynamic summarySheet = null;
                try
                {
                    summarySheet = summarySheets[1];
                    summarySheet.Name = "图层";
                    CreateLayerSummarySheet(summarySheet, layers);
                }
                finally
                {
                    ReleaseComObject(summarySheets);
                }

                if (summarySheet != null)
                {
                    worksheetsToRelease.Add(summarySheet);
                }

                var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "图层" };
                foreach (var layer in layers)
                {
                    token.ThrowIfCancellationRequested();

                    var sheetName = BuildUniqueSheetName(SanitizeSheetName(layer.Name), usedSheetNames);
                    dynamic sheets = workbook.Sheets;
                    dynamic fieldSheet = null;
                    try
                    {
                        dynamic afterSheet = sheets[sheets.Count];
                        try
                        {
                            fieldSheet = sheets.Add(After: afterSheet);
                        }
                        finally
                        {
                            ReleaseComObject(afterSheet);
                        }

                        fieldSheet.Name = sheetName;
                        CreateFieldSheet(fieldSheet, layer);
                    }
                    finally
                    {
                        ReleaseComObject(sheets);
                    }

                    if (fieldSheet != null)
                    {
                        worksheetsToRelease.Add(fieldSheet);
                    }

                    usedSheetNames.Add(sheetName);
                }

                if (outputPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    workbook.SaveAs(outputPath, xlOpenXMLWorkbook);
                }
                else
                {
                    workbook.SaveAs(outputPath, xlWorkbookNormal);
                }

                LogInfo("Excel文件保存完成。");
            }
            finally
            {
                foreach (var ws in worksheetsToRelease)
                {
                    ReleaseComObject(ws);
                }

                try
                {
                    if (workbook != null)
                    {
                        workbook.Close(false);
                    }
                }
                catch { }
                finally
                {
                    ReleaseComObject(workbook);
                }

                try
                {
                    if (excelApp != null)
                    {
                        excelApp.Quit();
                    }
                }
                catch { }
                finally
                {
                    ReleaseComObject(excelApp);
                }
            }
        }

        private Task RunExportOnStaThreadAsync(List<ShpLayerSchemaInfo> layers, string outputPath, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var thread = new Thread(() =>
            {
                try
                {
                    ExportToExcel(layers, outputPath, token);
                    tcs.TrySetResult(true);
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled(token);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return tcs.Task;
        }

        private string SanitizeSheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Sheet";
            }

            var invalidChars = new[] { '\\', '/', '?', '*', '[', ']', ':' };
            foreach (var c in invalidChars)
            {
                name = name.Replace(c, '_');
            }

            return name.Length > 31 ? name.Substring(0, 31) : name;
        }

        private string BuildUniqueSheetName(string baseName, HashSet<string> usedNames)
        {
            var name = baseName;
            int index = 1;
            while (usedNames.Contains(name))
            {
                var suffix = "_" + index;
                var prefix = baseName.Length + suffix.Length > 31
                    ? baseName.Substring(0, Math.Max(1, 31 - suffix.Length))
                    : baseName;
                name = prefix + suffix;
                index++;
            }

            return name;
        }

        private void CreateLayerSummarySheet(dynamic sheet, List<ShpLayerSchemaInfo> layers)
        {
            var headers = new[] { "序号", "图层别名", "几何类型", "属性表名", "约束条件", "备注" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1] = headers[i];
            }

            dynamic headerRange = sheet.Range[sheet.Cells[1, 1], sheet.Cells[1, headers.Length]];
            try
            {
                headerRange.Font.Bold = true;
                headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                headerRange.Borders.LineStyle = 1;
                headerRange.Borders.Weight = 2;
            }
            finally
            {
                ReleaseComObject(headerRange);
            }

            for (int i = 0; i < layers.Count; i++)
            {
                var row = i + 2;
                sheet.Cells[row, 1] = i + 1;
                sheet.Cells[row, 2] = string.IsNullOrWhiteSpace(layers[i].AliasName) ? layers[i].Name : layers[i].AliasName;
                sheet.Cells[row, 3] = layers[i].GeometryType;
                sheet.Cells[row, 4] = layers[i].Name;
                sheet.Cells[row, 5] = "";
                sheet.Cells[row, 6] = "";
            }

            if (layers.Count > 0)
            {
                dynamic dataRange = sheet.Range[sheet.Cells[2, 1], sheet.Cells[layers.Count + 1, headers.Length]];
                try
                {
                    dataRange.Borders.LineStyle = 1;
                    dataRange.Borders.Weight = 2;
                }
                finally
                {
                    ReleaseComObject(dataRange);
                }
            }

            sheet.Columns.AutoFit();
        }

        private void CreateFieldSheet(dynamic sheet, ShpLayerSchemaInfo layer)
        {
            var headers = new[] { "序号", "字段别名", "字段代码", "字段类型", "字段长度", "小数位数", "值域", "约束条件" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1] = headers[i];
            }

            dynamic headerRange = sheet.Range[sheet.Cells[1, 1], sheet.Cells[1, headers.Length]];
            try
            {
                headerRange.Font.Bold = true;
                headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                headerRange.Borders.LineStyle = 1;
                headerRange.Borders.Weight = 2;
            }
            finally
            {
                ReleaseComObject(headerRange);
            }

            for (int i = 0; i < layer.Fields.Count; i++)
            {
                var field = layer.Fields[i];
                var row = i + 2;
                sheet.Cells[row, 1] = i + 1;
                sheet.Cells[row, 2] = string.IsNullOrWhiteSpace(field.AliasName) ? field.FieldName : field.AliasName;
                sheet.Cells[row, 3] = field.FieldName;
                sheet.Cells[row, 4] = field.FieldType;
                sheet.Cells[row, 5] = field.Length?.ToString() ?? "";
                sheet.Cells[row, 6] = field.Scale?.ToString() ?? "";
                sheet.Cells[row, 7] = "";
                sheet.Cells[row, 8] = "";
            }

            if (layer.Fields.Count > 0)
            {
                dynamic dataRange = sheet.Range[sheet.Cells[2, 1], sheet.Cells[layer.Fields.Count + 1, headers.Length]];
                try
                {
                    dataRange.Borders.LineStyle = 1;
                    dataRange.Borders.Weight = 2;
                }
                finally
                {
                    ReleaseComObject(dataRange);
                }
            }

            sheet.Columns.AutoFit();
        }

        private void ReleaseComObject(object comObject)
        {
            try
            {
                if (comObject != null && Marshal.IsComObject(comObject))
                {
                    Marshal.FinalReleaseComObject(comObject);
                }
            }
            catch
            {
                // ignore
            }
        }

        private void StopExport()
        {
            _cancellationTokenSource?.Cancel();
            LogWarning("正在停止操作...");
        }

        private void ShowHelp()
        {
            var helpText = "【SHP输字段表】\n\n"
                         + "功能说明：\n"
                         + "将输入目录中的SHP字段结构导出为Excel表，可直接用于属性表建SHP模板编辑。\n\n"
                         + "参数说明：\n"
                         + "- 输入SHP目录：包含SHP文件的文件夹\n"
                         + "- 输出表：导出的Excel文件路径（.xlsx/.xls）\n\n"
                         + "输出结构：\n"
                         + "- 图层工作表：记录图层名、几何类型、属性表名\n"
                         + "- 字段工作表：每个SHP一个同名字段定义工作表\n\n"
                         + "注意事项：\n"
                         + "- 仅处理目录下一级SHP文件（不递归子目录）\n"
                         + "- 系统字段（FID/Shape等）不会导出\n"
                         + "- 工作表名称超过31字符会自动截断并去重";

            MessageBox.Show(helpText, "帮助 - SHP输字段表", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        private void LogInfo(string message)
        {
            AppendLog(message);
        }

        private void LogWarning(string message)
        {
            AppendLog("警告: " + message);
        }

        private void LogError(string message)
        {
            AppendLog("错误: " + message);
        }

        private void AppendLog(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                LogText += line;
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => LogText += line);
            }
        }
    }
}
