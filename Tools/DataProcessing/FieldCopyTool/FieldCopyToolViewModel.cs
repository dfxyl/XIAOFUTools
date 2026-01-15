using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using System.Threading;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace XIAOFUTools.Tools.FieldCopyTool
{
    /// <summary>
    /// 字段信息类，用于存储字段名和别名，支持选择状态
    /// </summary>
    public class FieldInfo : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// 字段名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 字段别名
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// 字段类型
        /// </summary>
        public FieldType FieldType { get; set; }

        /// <summary>
        /// 字段长度
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 显示文本，格式为"字段名称(别名)"
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(Alias) || Name == Alias)
                    return Name;
                else
                    return $"{Name}({Alias})";
            }
        }

        /// <summary>
        /// 重写ToString方法，返回显示文本
        /// </summary>
        public override string ToString()
        {
            return DisplayName;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 图层信息类，支持选择状态
    /// </summary>
    public class LayerInfo : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// 图层对象
        /// </summary>
        public FeatureLayer Layer { get; set; }

        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 图层名称
        /// </summary>
        public string Name => Layer?.Name ?? "";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 字段复制工具视图模型
    /// </summary>
    public class FieldCopyToolViewModel : PropertyChangedBase
    {
        #region 私有字段

        private ObservableCollection<FeatureLayer> _sourceLayerList;
        private ObservableCollection<FieldInfo> _fieldList;
        private ObservableCollection<LayerInfo> _targetLayerList;
        private FeatureLayer _selectedSourceLayer;
        private bool _isProcessing;
        private string _logText;

        #endregion

        #region 公共属性

        /// <summary>
        /// 源图层列表
        /// </summary>
        public ObservableCollection<FeatureLayer> SourceLayerList
        {
            get { return _sourceLayerList; }
            set
            {
                SetProperty(ref _sourceLayerList, value);
            }
        }

        /// <summary>
        /// 字段列表
        /// </summary>
        public ObservableCollection<FieldInfo> FieldList
        {
            get { return _fieldList; }
            set
            {
                SetProperty(ref _fieldList, value);
            }
        }

        /// <summary>
        /// 目标图层列表
        /// </summary>
        public ObservableCollection<LayerInfo> TargetLayerList
        {
            get { return _targetLayerList; }
            set
            {
                SetProperty(ref _targetLayerList, value);
            }
        }

        /// <summary>
        /// 选中的源图层
        /// </summary>
        public FeatureLayer SelectedSourceLayer
        {
            get { return _selectedSourceLayer; }
            set
            {
                SetProperty(ref _selectedSourceLayer, value);
                LoadFields();
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

        #endregion

        #region 命令

        /// <summary>
        /// 字段全选命令
        /// </summary>
        public ICommand SelectAllFieldsCommand { get; private set; }

        /// <summary>
        /// 字段反选命令
        /// </summary>
        public ICommand InvertFieldSelectionCommand { get; private set; }

        /// <summary>
        /// 目标图层全选命令
        /// </summary>
        public ICommand SelectAllTargetLayersCommand { get; private set; }

        /// <summary>
        /// 目标图层反选命令
        /// </summary>
        public ICommand InvertTargetLayerSelectionCommand { get; private set; }

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
        /// 刷新图层命令
        /// </summary>
        public ICommand RefreshLayersCommand { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public FieldCopyToolViewModel()
        {
            Initialize();
            InitializeCommands();
            LoadLayers();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化基本属性
        /// </summary>
        private void Initialize()
        {
            _sourceLayerList = new ObservableCollection<FeatureLayer>();
            _fieldList = new ObservableCollection<FieldInfo>();
            _targetLayerList = new ObservableCollection<LayerInfo>();
            _isProcessing = false;
            _logText = "";
        }

        /// <summary>
        /// 初始化命令
        /// </summary>
        private void InitializeCommands()
        {
            SelectAllFieldsCommand = new RelayCommand(() => SelectAllFields(), () => !IsProcessing);
            InvertFieldSelectionCommand = new RelayCommand(() => InvertFieldSelection(), () => !IsProcessing);
            SelectAllTargetLayersCommand = new RelayCommand(() => SelectAllTargetLayers(), () => !IsProcessing);
            InvertTargetLayerSelectionCommand = new RelayCommand(() => InvertTargetLayerSelection(), () => !IsProcessing);
            StartCommand = new RelayCommand(() => StartCopyFields(), () => CanStartCopy());
            StopCommand = new RelayCommand(() => StopCopyFields(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
            RefreshLayersCommand = new RelayCommand(() => RefreshLayers());
        }

        /// <summary>
        /// 加载图层
        /// </summary>
        private async void LoadLayers()
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    var map = MapView.Active?.Map;
                    if (map == null) return;

                    var featureLayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 加载源图层列表
                        SourceLayerList.Clear();
                        foreach (var layer in featureLayers)
                        {
                            SourceLayerList.Add(layer);
                        }

                        // 加载目标图层列表
                        TargetLayerList.Clear();
                        foreach (var layer in featureLayers)
                        {
                            TargetLayerList.Add(new LayerInfo { Layer = layer });
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogError($"加载图层时出错: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            LogInfo("正在刷新图层列表...");
            LoadLayers();
        }

        /// <summary>
        /// 加载字段
        /// </summary>
        private async void LoadFields()
        {
            if (SelectedSourceLayer == null)
            {
                FieldList.Clear();
                return;
            }

            await QueuedTask.Run(() =>
            {
                try
                {
                    using (var table = SelectedSourceLayer.GetTable())
                    {
                        var tableDefinition = table.GetDefinition();
                        var fields = tableDefinition.GetFields();

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            FieldList.Clear();
                            foreach (var field in fields)
                            {
                                // 排除系统字段（按名称）
                                if (field.Name.ToUpper() == "OBJECTID" ||
                                    field.Name.ToUpper() == "SHAPE" ||
                                    field.Name.ToUpper() == "SHAPE_LENGTH" ||
                                    field.Name.ToUpper() == "SHAPE_AREA")
                                    continue;

                                // 排除不支持的字段类型
                                if (field.FieldType == FieldType.Geometry ||
                                    field.FieldType == FieldType.OID ||
                                    field.FieldType == FieldType.GlobalID)
                                    continue;

                                FieldList.Add(new FieldInfo
                                {
                                    Name = field.Name,
                                    Alias = field.AliasName,
                                    FieldType = field.FieldType,
                                    Length = field.Length,
                                    IsSelected = false
                                });
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogError($"加载字段时出错: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 字段全选
        /// </summary>
        private void SelectAllFields()
        {
            foreach (var field in FieldList)
            {
                field.IsSelected = true;
            }
        }

        /// <summary>
        /// 字段反选
        /// </summary>
        private void InvertFieldSelection()
        {
            foreach (var field in FieldList)
            {
                field.IsSelected = !field.IsSelected;
            }
        }

        /// <summary>
        /// 目标图层全选
        /// </summary>
        private void SelectAllTargetLayers()
        {
            foreach (var layer in TargetLayerList)
            {
                layer.IsSelected = true;
            }
        }

        /// <summary>
        /// 目标图层反选
        /// </summary>
        private void InvertTargetLayerSelection()
        {
            foreach (var layer in TargetLayerList)
            {
                layer.IsSelected = !layer.IsSelected;
            }
        }

        /// <summary>
        /// 判断是否可以开始复制
        /// </summary>
        private bool CanStartCopy()
        {
            return !IsProcessing &&
                   SelectedSourceLayer != null &&
                   FieldList.Any(f => f.IsSelected) &&
                   TargetLayerList.Any(l => l.IsSelected);
        }

        /// <summary>
        /// 开始复制字段
        /// </summary>
        private async void StartCopyFields()
        {
            IsProcessing = true;
            LogText = "";
            LogInfo("开始复制字段...");

            try
            {
                var selectedFields = FieldList.Where(f => f.IsSelected).ToList();
                var selectedTargetLayers = TargetLayerList.Where(l => l.IsSelected).ToList();

                LogInfo($"源图层: {SelectedSourceLayer.Name}");
                LogInfo($"选中字段数: {selectedFields.Count}");
                LogInfo($"目标图层数: {selectedTargetLayers.Count}");

                await QueuedTask.Run(async () =>
                {
                    foreach (var targetLayerInfo in selectedTargetLayers)
                    {
                        var targetLayer = targetLayerInfo.Layer;
                        LogInfo($"正在处理目标图层: {targetLayer.Name}");

                        try
                        {
                            using (var targetTable = targetLayer.GetTable())
                            {
                                var targetDefinition = targetTable.GetDefinition();
                                var existingFields = targetDefinition.GetFields().ToDictionary(f => f.Name.ToUpper(), f => f);

                                // 收集要添加的字段
                                var fieldsToAdd = new List<ArcGIS.Core.Data.DDL.FieldDescription>();

                                foreach (var fieldInfo in selectedFields)
                                {
                                    if (existingFields.ContainsKey(fieldInfo.Name.ToUpper()))
                                    {
                                        LogWarning($"字段 {fieldInfo.Name} 在图层 {targetLayer.Name} 中已存在，跳过");
                                        continue;
                                    }

                                    // 创建新字段描述
                                    var fieldDescription = new ArcGIS.Core.Data.DDL.FieldDescription(fieldInfo.Name, fieldInfo.FieldType);
                                    fieldDescription.AliasName = fieldInfo.Alias;

                                    // 保留源字段的长度（如果有）
                                    if (fieldInfo.FieldType == FieldType.String && fieldInfo.Length > 0)
                                    {
                                        fieldDescription.Length = fieldInfo.Length;
                                    }
                                    else if (fieldInfo.FieldType == FieldType.String)
                                    {
                                        fieldDescription.Length = 255; // 默认字符串长度
                                    }

                                    fieldsToAdd.Add(fieldDescription);
                                }

                                if (fieldsToAdd.Count > 0)
                                {
                                    // 使用Geoprocessing工具添加字段，支持所有数据源类型（Shapefile、Geodatabase等）
                                    int successCount = 0;
                                    int failCount = 0;

                                    // 对于已加载到地图的图层，直接使用图层名称
                                    string targetLayerName = targetLayer.Name;

                                    foreach (var fieldDesc in fieldsToAdd)
                                    {
                                        try
                                        {
                                            // 将FieldType转换为Geoprocessing工具所需的字符串类型
                                            string fieldType = ConvertFieldTypeToString(fieldDesc.FieldType);
                                            
                                            // 构建AddField_management工具的参数
                                            var parameters = Geoprocessing.MakeValueArray(
                                                targetLayerName,
                                                fieldDesc.Name,
                                                fieldType,
                                                null, // precision
                                                null, // scale
                                                fieldDesc.Length > 0 ? (object)fieldDesc.Length : null, // length
                                                fieldDesc.AliasName // alias
                                            );

                                            var result = await Geoprocessing.ExecuteToolAsync("AddField_management", parameters);
                                            
                                            if (!result.IsFailed)
                                            {
                                                successCount++;
                                            }
                                            else
                                            {
                                                failCount++;
                                                var errorMsg = string.Join("; ", result.Messages.Where(m => m.Type == GPMessageType.Error).Select(m => m.Text));
                                                LogWarning($"字段 {fieldDesc.Name} 添加失败: {errorMsg}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            failCount++;
                                            LogError($"添加字段 {fieldDesc.Name} 时出错: {ex.Message}");
                                        }
                                    }

                                    if (successCount > 0)
                                    {
                                        LogInfo($"成功在图层 {targetLayer.Name} 中添加 {successCount} 个字段");
                                    }
                                    if (failCount > 0)
                                    {
                                        LogWarning($"图层 {targetLayer.Name} 中有 {failCount} 个字段添加失败");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError($"处理图层 {targetLayer.Name} 时出错: {ex.Message}");
                        }
                    }
                });

                LogInfo("字段复制完成！");
            }
            catch (Exception ex)
            {
                LogError($"复制字段时出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// 停止复制字段
        /// </summary>
        private void StopCopyFields()
        {
            // 这里可以实现取消逻辑
            IsProcessing = false;
            LogWarning("操作已停止");
        }



        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "字段复制工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于将源数据图层的字段复制到目标图层中。\n" +
                               "支持Shapefile、File Geodatabase、Enterprise Geodatabase等多种数据源。\n\n" +
                               "参数说明：\n" +
                               "1. 源数据：选择要复制字段的源图层\n" +
                               "2. 字段列表：显示源图层的所有字段，可多选\n" +
                               "3. 目标图层：选择要添加字段的目标图层，可多选\n\n" +
                               "操作步骤：\n" +
                               "1. 选择源数据图层\n" +
                               "2. 在字段列表中勾选要复制的字段\n" +
                               "3. 在目标图层列表中勾选要添加字段的图层\n" +
                               "4. 点击开始按钮执行复制操作\n\n" +
                               "注意事项：\n" +
                               "- 如果目标图层中已存在同名字段，将跳过该字段\n" +
                               "- 系统字段（OBJECTID、SHAPE等）和几何字段不会显示在字段列表中\n" +
                               "- 不支持复制几何字段、OID字段和GlobalID字段\n" +
                               "- 字符串字段的默认长度为255\n" +
                               "- 操作过程和结果将显示在日志窗口中";

            MessageBox.Show(helpContent, "字段复制工具使用说明");
        }

        /// <summary>
        /// 将FieldType转换为Geoprocessing工具所需的字符串类型
        /// </summary>
        private string ConvertFieldTypeToString(FieldType fieldType)
        {
            switch (fieldType)
            {
                case FieldType.SmallInteger:
                    return "SHORT";
                case FieldType.Integer:
                    return "LONG";
                case FieldType.Single:
                    return "FLOAT";
                case FieldType.Double:
                    return "DOUBLE";
                case FieldType.String:
                    return "TEXT";
                case FieldType.Date:
                    return "DATE";
                case FieldType.Blob:
                    return "BLOB";
                case FieldType.Raster:
                    return "RASTER";
                case FieldType.GUID:
                    return "GUID";
                default:
                    return "TEXT"; // 默认为文本类型
            }
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] {message}\n";
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] 警告: {message}\n";
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] 错误: {message}\n";
        }

        #endregion
    }
}
