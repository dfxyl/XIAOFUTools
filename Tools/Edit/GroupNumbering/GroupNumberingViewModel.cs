using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

namespace XIAOFUTools.Tools.GroupNumbering
{
    /// <summary>
    /// 字段信息类，用于存储字段名和别名
    /// </summary>
    public class FieldInfo
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 字段别名
        /// </summary>
        public string Alias { get; set; }

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
    }

    /// <summary>
    /// 分组编号ViewModel
    /// </summary>
    internal class GroupNumberingViewModel : PropertyChangedBase
    {
        #region 属性

        private ObservableCollection<FeatureLayer> _layerList;
        public ObservableCollection<FeatureLayer> LayerList
        {
            get { return _layerList; }
            set
            {
                SetProperty(ref _layerList, value);
            }
        }

        private FeatureLayer _selectedLayer;
        public FeatureLayer SelectedLayer
        {
            get { return _selectedLayer; }
            set
            {
                var oldLayer = _selectedLayer;
                SetProperty(ref _selectedLayer, value);
                if (_selectedLayer != null && _selectedLayer != oldLayer)
                {
                    // 加载新选图层的字段
                    LoadFields();
                    // 更新选择信息
                    UpdateSelectionInfo();
                }
            }
        }

        private ObservableCollection<FieldInfo> _fieldList;
        public ObservableCollection<FieldInfo> FieldList
        {
            get { return _fieldList; }
            set
            {
                SetProperty(ref _fieldList, value);
            }
        }

        private ObservableCollection<object> _groupFieldList;
        public ObservableCollection<object> GroupFieldList
        {
            get { return _groupFieldList; }
            set
            {
                SetProperty(ref _groupFieldList, value);
            }
        }

        private ObservableCollection<FieldInfo> _numberFieldList;
        public ObservableCollection<FieldInfo> NumberFieldList
        {
            get { return _numberFieldList; }
            set
            {
                SetProperty(ref _numberFieldList, value);
            }
        }

        private object _selectedGroupField;
        public object SelectedGroupField
        {
            get { return _selectedGroupField; }
            set
            {
                SetProperty(ref _selectedGroupField, value);
                // 不影响编号字段的选择
            }
        }

        private FieldInfo _selectedNumberField;
        public FieldInfo SelectedNumberField
        {
            get { return _selectedNumberField; }
            set
            {
                SetProperty(ref _selectedNumberField, value);
                // 不影响分组字段的选择
            }
        }

        private ObservableCollection<int> _digitsList;
        public ObservableCollection<int> DigitsList
        {
            get { return _digitsList; }
            set
            {
                SetProperty(ref _digitsList, value);
            }
        }

        private int _selectedDigit;
        public int SelectedDigit
        {
            get { return _selectedDigit; }
            set
            {
                SetProperty(ref _selectedDigit, value);
            }
        }

        private int _startNumber;
        public int StartNumber
        {
            get { return _startNumber; }
            set
            {
                SetProperty(ref _startNumber, value);
            }
        }

        private string _prefix;
        public string Prefix
        {
            get { return _prefix; }
            set
            {
                SetProperty(ref _prefix, value);
            }
        }

        private string _suffix;
        public string Suffix
        {
            get { return _suffix; }
            set
            {
                SetProperty(ref _suffix, value);
            }
        }

        private bool _onlyEmptyRecords;
        public bool OnlyEmptyRecords
        {
            get { return _onlyEmptyRecords; }
            set
            {
                SetProperty(ref _onlyEmptyRecords, value);
            }
        }

        private bool _useSelection = true;
        public bool UseSelection
        {
            get { return _useSelection; }
            set { SetProperty(ref _useSelection, value); }
        }

        private bool _hasSelection;
        public bool HasSelection
        {
            get { return _hasSelection; }
            set { SetProperty(ref _hasSelection, value); }
        }

        private int _selectedCount;
        public int SelectedCount
        {
            get { return _selectedCount; }
            set { SetProperty(ref _selectedCount, value); }
        }

        private string _selectionInfoText;
        public string SelectionInfoText
        {
            get { return _selectionInfoText; }
            set
            {
                SetProperty(ref _selectionInfoText, value);
            }
        }

        #endregion

        #region 命令

        private ICommand _executeCommand;
        public ICommand ExecuteCommand
        {
            get
            {
                if (_executeCommand == null)
                {
                    _executeCommand = new RelayCommand(() => ExecuteNumbering(), () => CanExecuteNumbering());
                }
                return _executeCommand;
            }
        }

        private ICommand _showHelpCommand;
        public ICommand ShowHelpCommand
        {
            get
            {
                if (_showHelpCommand == null)
                {
                    _showHelpCommand = new RelayCommand(() => ShowHelp());
                }
                return _showHelpCommand;
            }
        }

        private ICommand _cancelCommand;
        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    _cancelCommand = new RelayCommand(() => CloseWindow());
                }
                return _cancelCommand;
            }
        }

        private ICommand _refreshLayersCommand;
        public ICommand RefreshLayersCommand
        {
            get
            {
                if (_refreshLayersCommand == null)
                {
                    _refreshLayersCommand = new RelayCommand(() => RefreshLayers());
                }
                return _refreshLayersCommand;
            }
        }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public GroupNumberingViewModel()
        {
            // 初始化
            Initialize();
            
            // 异步加载图层，但不等待其完成
            LoadLayersAsync();

            // 订阅地图选择变化事件，实时更新选择信息
            MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
        }

        /// <summary>
        /// 刷新图层列表（供DockPane刷新按钮调用）
        /// </summary>
        public void RefreshLayers()
        {
            LoadLayersAsync();
        }

        /// <summary>
        /// 初始化基本属性
        /// </summary>
        private void Initialize()
        {
            // 初始化默认值
            _prefix = string.Empty;
            _suffix = string.Empty;
            _onlyEmptyRecords = false;
            _startNumber = 1; // 默认起始号码为1
            
            // 确保在UI线程上初始化集合
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // 初始化空的图层列表
                _layerList = new ObservableCollection<FeatureLayer>();
                NotifyPropertyChanged(() => LayerList);
                
                // 初始化空的字段列表
                _fieldList = new ObservableCollection<FieldInfo>();
                NotifyPropertyChanged(() => FieldList);
                
                // 初始化空的分组字段列表
                _groupFieldList = new ObservableCollection<object>();
                NotifyPropertyChanged(() => GroupFieldList);
                
                // 初始化空的编号字段列表
                _numberFieldList = new ObservableCollection<FieldInfo>();
                NotifyPropertyChanged(() => NumberFieldList);
                
                // 初始化有效位数列表
                _digitsList = new ObservableCollection<int> { 1, 2, 3, 4, 5 };
                NotifyPropertyChanged(() => DigitsList);
                
                // 默认选择2位数
                _selectedDigit = 2;
                NotifyPropertyChanged(() => SelectedDigit);
            });
        }

        /// <summary>
        /// 异步加载图层，不阻塞UI线程
        /// </summary>
        private async void LoadLayersAsync()
        {
            try
            {
                // 使用QueuedTask在后台线程执行
                var featureLayers = await QueuedTask.Run(() =>
                {
                    var tempLayers = new List<FeatureLayer>();
                    
                    // 获取当前活动地图视图
                    var mapView = MapView.Active;
                    if (mapView == null || mapView.Map == null)
                    {
                        return tempLayers;
                    }

                    // 获取地图中的所有图层
                    var allLayers = mapView.Map.GetLayersAsFlattenedList();
                    if (allLayers == null || !allLayers.Any())
                    {
                        return tempLayers;
                    }

                    // 筛选出要素图层
                    var layers = allLayers.OfType<FeatureLayer>().ToList();
                    if (layers != null && layers.Any())
                    {
                        tempLayers.AddRange(layers);
                    }
                    
                    return tempLayers;
                });

                // 在UI线程上更新ObservableCollection
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 清空当前列表
                    _layerList.Clear();
                    
                    // 添加图层到列表
                    foreach (var layer in featureLayers)
                    {
                        _layerList.Add(layer);
                    }
                    
                    // 通知属性变化
                    NotifyPropertyChanged(() => LayerList);
                    
                    // 如果列表不为空，选择第一个图层
                    if (_layerList.Count > 0 && _selectedLayer == null)
                    {
                        SelectedLayer = _layerList[0];
                    }

                    // 更新一次选择信息（即使未选择图层也会显示默认提示）
                    UpdateSelectionInfo();
                });
            }
            catch (Exception ex)
            {
                // 捕获并显示加载图层时的任何异常
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"加载图层时出错: {ex.Message}", "错误");
            }

            // 通知界面更新
            NotifyPropertyChanged(() => LayerList);
        }

        /// <summary>
        /// 加载所选图层的字段
        /// </summary>
        private async void LoadFields()
        {
            // 检查所选图层是否为null
            if (_selectedLayer == null)
                return;

            try
            {
                // 先通知UI更新字段列表
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 清空通用字段列表
                    if (_fieldList == null)
                    {
                        _fieldList = new ObservableCollection<FieldInfo>();
                    }
                    else
                    {
                        _fieldList.Clear();
                    }
                    
                    // 清空分组字段列表
                    if (_groupFieldList == null)
                    {
                        _groupFieldList = new ObservableCollection<object>();
                    }
                    else
                    {
                        _groupFieldList.Clear();
                    }
                    
                    // 清空编号字段列表
                    if (_numberFieldList == null)
                    {
                        _numberFieldList = new ObservableCollection<FieldInfo>();
                    }
                    else
                    {
                        _numberFieldList.Clear();
                    }
                    
                    // 清空当前选择的字段
                    _selectedGroupField = null;
                    _selectedNumberField = null;
                    
                    NotifyPropertyChanged(() => FieldList);
                    NotifyPropertyChanged(() => GroupFieldList);
                    NotifyPropertyChanged(() => NumberFieldList);
                    NotifyPropertyChanged(() => SelectedGroupField);
                    NotifyPropertyChanged(() => SelectedNumberField);
                });

                // 在后台线程中获取字段
                var fieldInfos = await QueuedTask.Run(() =>
                {
                    var tempFields = new List<FieldInfo>();
                    
                    // 获取图层的字段信息
                    var table = _selectedLayer.GetTable();
                    if (table == null)
                        return tempFields;
                        
                    var definition = table.GetDefinition();
                    if (definition == null)
                        return tempFields;
                        
                    var fields = definition.GetFields();
                    if (fields == null)
                        return tempFields;

                    // 添加支持的字段类型
                    foreach (var field in fields)
                    {
                        if (field == null)
                            continue;
                            
                        // 添加文本和数字类型的字段
                        if (field.FieldType == FieldType.String || 
                            field.FieldType == FieldType.SmallInteger || 
                            field.FieldType == FieldType.Integer ||
                            field.FieldType == FieldType.Double)
                        {
                            tempFields.Add(new FieldInfo 
                            { 
                                Name = field.Name, 
                                Alias = field.AliasName 
                            });
                        }
                    }
                    
                    return tempFields;
                });
                
                // 在UI线程上更新字段列表
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 先向分组字段列表添加一个空选项
                    _groupFieldList.Add("(不分组)");
                    
                    // 更新所有字段列表
                    foreach (var fieldInfo in fieldInfos)
                    {
                        _fieldList.Add(fieldInfo);
                        _groupFieldList.Add(fieldInfo);
                        _numberFieldList.Add(fieldInfo);
                    }
                    
                    // 通知界面更新
                    NotifyPropertyChanged(() => FieldList);
                    NotifyPropertyChanged(() => GroupFieldList);
                    NotifyPropertyChanged(() => NumberFieldList);
                    
                    // 默认选择"不分组"选项
                    _selectedGroupField = "(不分组)";
                    NotifyPropertyChanged(() => SelectedGroupField);
                });
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"加载字段时出错: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 更新所选要素数量的提示信息
        /// </summary>
        private void UpdateSelectionInfo()
        {
            Task.Run(async () =>
            {
                int count = 0;
                await QueuedTask.Run(() =>
                {
                    if (_selectedLayer != null)
                    {
                        count = _selectedLayer.SelectionCount;
                    }
                });

                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (count > 0)
                    {
                        // 如果从无选择变为有选择，默认开启使用选择
                        if (!HasSelection && !_useSelection)
                            UseSelection = true;
                        HasSelection = true;
                        SelectedCount = count;
                        SelectionInfoText = $"已选 {count}";
                    }
                    else
                    {
                        HasSelection = false;
                        if (_useSelection)
                            UseSelection = false; // 无选择时强制关闭
                        SelectedCount = 0;
                        SelectionInfoText = "全部要素";
                    }
                });
            });
        }

        /// <summary>
        /// 地图选择变化事件
        /// </summary>
        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            // 简单处理：任意选择变化均尝试刷新显示
            UpdateSelectionInfo();
        }

        /// <summary>
        /// 执行编号操作
        /// </summary>
        private async void ExecuteNumbering()
        {
            if (!CanExecuteNumbering())
                return;

            try
            {
                // 显示进度对话框
                ProgressDialog progressDialog = new ProgressDialog("正在执行分组编号...");
                progressDialog.Show();

                // 在后台线程执行编号操作
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        // 创建编辑操作
                        var editOperation = new EditOperation();
                        editOperation.Name = "分组编号";
                        
                        // 获取选中图层的要素表格
                        var featureTable = _selectedLayer.GetTable();
                        
                        // 准备查询
                        var queryFilter = new QueryFilter();
                        
                        // 如果只编号空记录，添加过滤条件
                        if (_onlyEmptyRecords)
                        {
                            queryFilter.WhereClause = $"{_selectedNumberField.Name} IS NULL OR {_selectedNumberField.Name} = ''";
                        }
                        
                        // 根据是否存在选择集决定数据源：优先在选择集中搜索
                        bool useSelection = false;
                        useSelection = _useSelection && _selectedLayer.SelectionCount > 0;

                        using (var rowCursor = useSelection
                            ? _selectedLayer.GetSelection().Search(queryFilter, false)
                            : featureTable.Search(queryFilter, false))
                        {
                            // 获取分组字段值和对应的要素OID
                            var groupValues = new Dictionary<string, List<long>>();
                            
                            // 检查是否有分组字段并且不是"不分组"选项
                            string groupFieldName = null;
                            if (_selectedGroupField is FieldInfo fieldInfo)
                            {
                                groupFieldName = fieldInfo.Name;
                            }
                            
                            while (rowCursor.MoveNext())
                            {
                                using (var row = rowCursor.Current)
                                {
                                    // 获取分组字段值
                                    string groupValue = "DefaultGroup"; // 默认组
                                    
                                    if (!string.IsNullOrEmpty(groupFieldName))
                                    {
                                        var groupValueObj = row[groupFieldName];
                                        groupValue = groupValueObj?.ToString() ?? "DefaultGroup";
                                    }
                                    
                                    // 添加到分组字典
                                    if (!groupValues.ContainsKey(groupValue))
                                    {
                                        groupValues[groupValue] = new List<long>();
                                    }
                                    
                                    groupValues[groupValue].Add(row.GetObjectID());
                                }
                            }
                            
                            // 对每个分组分别编号
                            foreach (var group in groupValues)
                            {
                                int currentNumber = _startNumber;
                                
                                foreach (var objectId in group.Value)
                                {
                                    // 格式化编号，根据有效位数补0
                                    string formattedNumber = currentNumber.ToString().PadLeft(_selectedDigit, '0');
                                    
                                    // 构建最终编号
                                    string finalNumber = $"{_prefix}{formattedNumber}{_suffix}";
                                    
                                    // 使用EditOperation来更新字段值
                                    editOperation.Modify(featureTable, objectId, 
                                        new Dictionary<string, object> { { _selectedNumberField.Name, finalNumber } });
                                    
                                    currentNumber++;
                                }
                            }
                        }
                        
                        // 执行编辑操作
                        bool result = editOperation.Execute();
                        if (!result)
                        {
                            throw new Exception("编号操作执行失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("执行编号时出错: " + ex.Message, "错误");
                    }
                });

                // 关闭进度对话框
                progressDialog.Dispose();
                
                // 刷新地图视图
                await QueuedTask.Run(() => 
                {
                    if (_selectedLayer != null)
                    {
                        _selectedLayer.ClearSelection();
                        // 直接刷新整个地图视图
                        if (MapView.Active != null)
                        {
                            MapView.Active.Redraw(true);
                        }
                    }
                });
                
                // 显示成功消息
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("分组编号操作已完成！", "完成");
                
                // 关闭窗口
                CloseWindow();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("执行编号时出错: " + ex.Message, "错误");
            }
        }

        /// <summary>
        /// 判断是否可以执行编号
        /// </summary>
        private bool CanExecuteNumbering()
        {
            // 必须选择图层和编号字段
            return _selectedLayer != null && _selectedNumberField != null;
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        private void CloseWindow()
        {
            // 使用FrameworkApplication查找窗口
            var windows = System.Windows.Application.Current.Windows;
            foreach (System.Windows.Window window in windows)
            {
                if (window.Title == "要素顺序编号")
                {
                    window.Close();
                    break;
                }
            }
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "要素顺序编号工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于为要素添加编号，可根据指定字段进行分组编号。\n\n" +
                               "参数说明：\n" +
                               "1. 编号图层：选择要进行编号的图层\n" +
                               "2. 分组字段：用于对要素进行分组的字段，选择\"不分组\"则不分组\n" +
                               "3. 编号字段：用于存储生成的编号的字段\n" +
                               "4. 有效位数：编号的位数，例如选择3，则编号为001、002...\n" +
                               "5. 起始号码：编号的起始值，例如设置为5，则编号从005开始\n" +
                               "6. 前缀：编号前的文本，例如\"编号\"\n" +
                               "7. 后缀：编号后的文本，例如\"号\"\n" +
                               "8. 只编辑空记录：选中时只对编号字段为空的要素进行编号\n\n" +
                               "操作步骤：\n" +
                               "1. 选择需要编号的图层\n" +
                               "2. 选择分组字段（可选）\n" +
                               "3. 选择编号字段\n" +
                               "4. 设置编号参数（位数、起始号码、前后缀等）\n" +
                               "5. 点击执行按钮进行编号\n\n" +
                               "注意事项：\n" +
                               "- 编号字段必须是文本类型\n" +
                               "- 操作不可恢复，请确认后再执行";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "要素顺序编号工具使用说明");
        }
    }
}

