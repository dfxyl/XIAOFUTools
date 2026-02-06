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

namespace XIAOFUTools.Tools.DataProcessing.MirrorDatabase
{
    /// <summary>
    /// 布尔值反转转换器
    /// </summary>
    public class BooleanInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return value;
        }
    }

    /// <summary>
    /// 镜像数据库视图模型
    /// </summary>
    public class MirrorDatabaseViewModel : PropertyChangedBase
    {
        #region 私有字段

        private string _sourceDatabasePath;
        private string _outputFolderPath;
        private string _databaseName;
        private bool _isProcessing;
        private string _logText;
        private CancellationTokenSource _cancellationTokenSource;

        #endregion

        #region 公共属性

        public string SourceDatabasePath
        {
            get { return _sourceDatabasePath; }
            set
            {
                SetProperty(ref _sourceDatabasePath, value);
                if (!string.IsNullOrEmpty(value))
                {
                    var dirName = Path.GetFileName(value);
                    if (dirName.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                    {
                        DatabaseName = dirName.Substring(0, dirName.Length - 4) + "_Mirror";
                    }
                }
            }
        }

        public string OutputFolderPath
        {
            get { return _outputFolderPath; }
            set { SetProperty(ref _outputFolderPath, value); }
        }

        public string DatabaseName
        {
            get { return _databaseName; }
            set { SetProperty(ref _databaseName, value); }
        }

        public bool IsProcessing
        {
            get { return _isProcessing; }
            set { SetProperty(ref _isProcessing, value); }
        }

        public string LogText
        {
            get { return _logText; }
            set { SetProperty(ref _logText, value); }
        }

        #endregion

        #region 命令

        public ICommand BrowseSourceDatabaseCommand { get; private set; }
        public ICommand BrowseOutputFolderCommand { get; private set; }
        public ICommand StartCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }

        #endregion

        #region 构造函数

        public MirrorDatabaseViewModel()
        {
            Initialize();
            InitializeCommands();
        }

        #endregion

        #region 私有方法

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

        private void BrowseSourceDatabase()
        {
            var openItemDialog = new OpenItemDialog
            {
                Title = "选择源文件地理数据库",
                MultiSelect = false,
                Filter = ItemFilters.Geodatabases
            };

            if (openItemDialog.ShowDialog() == true && openItemDialog.Items.Any())
            {
                SourceDatabasePath = openItemDialog.Items.First().Path;
                LogInfo("已选择源数据库: " + SourceDatabasePath);
            }
        }

        private void BrowseOutputFolder()
        {
            var folderDialog = new OpenItemDialog
            {
                Title = "选择输出位置",
                MultiSelect = false,
                Filter = ItemFilters.Folders
            };

            if (folderDialog.ShowDialog() == true && folderDialog.Items.Any())
            {
                OutputFolderPath = folderDialog.Items.First().Path;
                LogInfo("已选择输出位置: " + OutputFolderPath);
            }
        }

        private bool CanStart()
        {
            return !IsProcessing &&
                   !string.IsNullOrWhiteSpace(SourceDatabasePath) &&
                   !string.IsNullOrWhiteSpace(OutputFolderPath) &&
                   !string.IsNullOrWhiteSpace(DatabaseName);
        }

        private async void StartMirrorDatabase()
        {
            IsProcessing = true;
            LogText = "";
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            var startTime = DateTime.Now;
            LogInfo("开始时间: " + startTime.ToString("yyyy年MM月dd日 HH:mm:ss"));

            try
            {
                if (!Directory.Exists(SourceDatabasePath))
                {
                    LogError("指定的源数据库不存在。");
                    return;
                }

                string targetGdbPath = Path.Combine(OutputFolderPath, DatabaseName + ".gdb");

                await QueuedTask.Run(() =>
                {
                    if (!Directory.Exists(targetGdbPath))
                    {
                        LogInfo("正在创建目标数据库: " + DatabaseName + ".gdb");
                        try
                        {
                            var args = Geoprocessing.MakeValueArray(OutputFolderPath, DatabaseName);
                            var result = Geoprocessing.ExecuteToolAsync("CreateFileGDB_management", args, null, null, null, GPExecuteToolFlags.None).Result;
                            if (result.IsFailed)
                            {
                                LogError("创建目标数据库失败");
                                return;
                            }
                            LogInfo("目标数据库 " + DatabaseName + ".gdb 创建成功");
                        }
                        catch (Exception ex)
                        {
                            LogError("创建目标数据库失败: " + ex.Message);
                            return;
                        }
                    }
                    else
                    {
                        LogInfo("目标数据库 " + DatabaseName + ".gdb 已存在，将在其中创建结构");
                    }

                    using (var sourceGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(SourceDatabasePath))))
                    using (var targetGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(targetGdbPath))))
                    {
                        var processedFCs = new HashSet<string>();

                        // 镜像要素集
                        var featureDatasetDefs = sourceGdb.GetDefinitions<FeatureDatasetDefinition>();
                        LogInfo("源数据库包含 " + featureDatasetDefs.Count() + " 个要素集");

                        foreach (var datasetDef in featureDatasetDefs)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                return;
                            }

                            string datasetName = datasetDef.GetName();
                            LogInfo("正在镜像要素集: " + datasetName);

                            try
                            {
                                bool exists = false;
                                try
                                {
                                    var existingDef = targetGdb.GetDefinition<FeatureDatasetDefinition>(datasetName);
                                    exists = existingDef != null;
                                }
                                catch { exists = false; }

                                if (!exists)
                                {
                                    var spatialRef = datasetDef.GetSpatialReference();
                                    var newDatasetDesc = new FeatureDatasetDescription(datasetName, spatialRef);
                                    var schemaBuilder = new SchemaBuilder(targetGdb);
                                    schemaBuilder.Create(newDatasetDesc);
                                    if (schemaBuilder.Build())
                                    {
                                        LogInfo("  要素集 " + datasetName + " 创建成功");
                                    }
                                    else
                                    {
                                        LogError("  创建要素集 " + datasetName + " 失败");
                                    }
                                }
                                else
                                {
                                    LogInfo("  要素集 " + datasetName + " 已存在");
                                }

                                // 镜像要素集内的要素类
                                using (var featureDataset = sourceGdb.OpenDataset<FeatureDataset>(datasetName))
                                {
                                    var fcDefs = featureDataset.GetDefinitions<FeatureClassDefinition>();
                                    foreach (var fcDef in fcDefs)
                                    {
                                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                                        {
                                            LogWarning("操作已被用户取消");
                                            return;
                                        }

                                        string fcName = fcDef.GetName();
                                        processedFCs.Add(fcName);
                                        LogInfo("  正在镜像要素类: " + fcName);

                                        try
                                        {
                                            bool fcExists = false;
                                            try
                                            {
                                                var existingDef = targetGdb.GetDefinition<FeatureClassDefinition>(fcName);
                                                fcExists = existingDef != null;
                                            }
                                            catch { fcExists = false; }

                                            if (!fcExists)
                                            {
                                                MirrorFeatureClassInDataset(targetGdb, fcDef, datasetName);
                                            }
                                            else
                                            {
                                                LogInfo("    要素类 " + fcName + " 已存在");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogError("    镜像要素类 " + fcName + " 失败: " + ex.Message);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError("  镜像要素集 " + datasetName + " 失败: " + ex.Message);
                            }
                        }

                        // 镜像根目录下的要素类
                        var featureClassDefs = sourceGdb.GetDefinitions<FeatureClassDefinition>();
                        int rootFcCount = 0;
                        foreach (var fcDef in featureClassDefs)
                        {
                            string fcName = fcDef.GetName();
                            if (!processedFCs.Contains(fcName))
                            {
                                rootFcCount++;
                            }
                        }
                        LogInfo("源数据库根目录包含 " + rootFcCount + " 个要素类");

                        foreach (var fcDef in featureClassDefs)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                return;
                            }

                            string fcName = fcDef.GetName();
                            if (!processedFCs.Contains(fcName))
                            {
                                LogInfo("正在镜像要素类: " + fcName);

                                try
                                {
                                    bool exists = false;
                                    try
                                    {
                                        var existingDef = targetGdb.GetDefinition<FeatureClassDefinition>(fcName);
                                        exists = existingDef != null;
                                    }
                                    catch { exists = false; }

                                    if (!exists)
                                    {
                                        MirrorFeatureClass(targetGdb, fcDef);
                                    }
                                    else
                                    {
                                        LogInfo("  要素类 " + fcName + " 已存在");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogError("  镜像要素类 " + fcName + " 失败: " + ex.Message);
                                }
                            }
                        }

                        // 镜像独立表
                        var tableDefs = sourceGdb.GetDefinitions<TableDefinition>();
                        var standaloneTables = tableDefs.Where(t => !(t is FeatureClassDefinition)).ToList();
                        LogInfo("源数据库包含 " + standaloneTables.Count + " 个独立表");

                        foreach (var tableDef in standaloneTables)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                return;
                            }

                            string tableName = tableDef.GetName();
                            LogInfo("正在镜像表: " + tableName);

                            try
                            {
                                bool exists = false;
                                try
                                {
                                    var existingDef = targetGdb.GetDefinition<TableDefinition>(tableName);
                                    exists = existingDef != null;
                                }
                                catch { exists = false; }

                                if (!exists)
                                {
                                    MirrorTable(targetGdb, tableDef);
                                }
                                else
                                {
                                    LogInfo("  表 " + tableName + " 已存在");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError("  镜像表 " + tableName + " 失败: " + ex.Message);
                            }
                        }
                    }
                });

                LogInfo("镜像完成！");
            }
            catch (Exception ex)
            {
                LogError("镜像过程中出错: " + ex.Message);
            }
            finally
            {
                var endTime = DateTime.Now;
                LogInfo("结束时间: " + endTime.ToString("yyyy年MM月dd日 HH:mm:ss"));
                LogInfo("历时: " + (endTime - startTime).ToString());
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
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

        private void ShowHelp()
        {
            string helpMessage = "【镜像数据库工具使用说明】\n\n" +
                "功能说明：\n" +
                "将源文件地理数据库的结构（要素集、要素类、表）镜像到新的数据库中，只复制结构不复制数据。\n\n" +
                "使用步骤：\n" +
                "1. 选择源数据库：点击浏览选择要镜像的.gdb文件\n" +
                "2. 选择输出路径：点击浏览选择新数据库的保存位置\n" +
                "3. 输入数据库名称：设置新数据库的名称（不含.gdb后缀）\n" +
                "4. 点击开始镜像执行操作\n\n" +
                "注意事项：\n" +
                "- 只镜像数据库结构，不复制数据\n" +
                "- 会保留字段名称、类型、别名等属性\n" +
                "- 会保留要素集结构\n" +
                "- 如果目标数据库已存在，会在其中创建不存在的结构";

            MessageBox.Show(helpMessage, "镜像数据库 - 帮助", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        #endregion

        #region 日志方法

        private void LogInfo(string message)
        {
            AppendLog("[信息] " + message);
        }

        private void LogWarning(string message)
        {
            AppendLog("[警告] " + message);
        }

        private void LogError(string message)
        {
            AppendLog("[错误] " + message);
        }

        private void AppendLog(string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogText += DateTime.Now.ToString("HH:mm:ss") + " " + message + "\r\n";
            });
        }

        #endregion
    }
}
