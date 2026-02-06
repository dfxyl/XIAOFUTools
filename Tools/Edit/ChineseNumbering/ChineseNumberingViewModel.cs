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
using ArcGIS.Desktop.Framework.Events;

namespace XIAOFUTools.Tools.ChineseNumbering
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
    /// 地块中文编号ViewModel
    /// </summary>
    internal class ChineseNumberingViewModel : PropertyChangedBase
    {
        private dynamic _mapSelectionChangedToken;

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

        // 新增分组字段列表属性
        private ObservableCollection<object> _groupFieldList;
        public ObservableCollection<object> GroupFieldList
        {
            get { return _groupFieldList; }
            set
            {
                SetProperty(ref _groupFieldList, value);
            }
        }

        // 新增分组字段选择属性
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
            }
        }

        private int _startNumber;
        public int StartNumber
        {
            get { return _startNumber; }
            set
            {
                SetProperty(ref _startNumber, value);
                // 更新中文表示
                ChineseStartNumber = ConvertToChinese(value);
            }
        }

        private string _chineseStartNumber;
        public string ChineseStartNumber
        {
            get { return _chineseStartNumber; }
            set
            {
                SetProperty(ref _chineseStartNumber, value);
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
        public ChineseNumberingViewModel()
        {
            // 初始化
            Initialize();

            // 异步加载图层，但不等待其完成
            LoadLayersAsync();

            // 订阅地图选择变化事件
            _mapSelectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
            // 初始化一次选择提示
            UpdateSelectionInfo();
        }

        /// <summary>
        /// 刷新图层列表（供DockPane调用）
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
            _chineseStartNumber = "一"; // 默认起始号码的中文表示
            
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
            });
        }

        /// <summary>
        /// 异步加载图层，不阻塞UI线程
        /// </summary>
        private void LoadLayersAsync()
        {
            Task.Run(async () =>
            {
                try
                {
                    var tempLayers = new List<FeatureLayer>();

                    await QueuedTask.Run(() =>
                    {
                        var map = MapView.Active?.Map;
                        if (map != null)
                        {
                            var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>();
                            foreach (var layer in layers)
                            {
                                tempLayers.Add(layer);
                            }
                        }
                    });

                    // 在UI线程更新图层列表
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 清空图层列表
                            LayerList?.Clear();

                            // 添加图层
                            if (LayerList != null)
                            {
                                foreach (var layer in tempLayers)
                                {
                                    LayerList.Add(layer);
                                }

                                // 如果有图层，默认选择第一个
                                if (LayerList.Count > 0 && SelectedLayer == null)
                                {
                                    SelectedLayer = LayerList[0];
                                }
                                // 更新一次选择信息
                                UpdateSelectionInfo();
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    // 确保在UI线程显示错误信息
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"加载图层时出错: {ex.Message}", "错误");
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 加载所选图层的字段
        /// </summary>
        private void LoadFields()
        {
            if (SelectedLayer == null)
            {
                FieldList?.Clear();
                GroupFieldList?.Clear();
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var tempFieldInfos = new List<FieldInfo>();

                    await QueuedTask.Run(() =>
                    {
                        var table = SelectedLayer.GetTable();
                        if (table != null)
                        {
                            var definition = table.GetDefinition();
                            var fields = definition.GetFields();

                            foreach (var field in fields)
                            {
                                // 添加文本和数字类型的字段
                                if (field.FieldType == FieldType.String ||
                                    field.FieldType == FieldType.SmallInteger ||
                                    field.FieldType == FieldType.Integer ||
                                    field.FieldType == FieldType.Double)
                                {
                                    var fieldInfo = new FieldInfo
                                    {
                                        Name = field.Name,
                                        Alias = field.AliasName
                                    };
                                    tempFieldInfos.Add(fieldInfo);
                                }
                            }
                        }
                    });

                    // 在UI线程更新字段列表
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            FieldList?.Clear();
                            GroupFieldList?.Clear();

                            if (FieldList != null && GroupFieldList != null)
                            {
                                // 先向分组字段列表添加一个空选项
                                GroupFieldList.Add("(不分组)");

                                // 更新字段列表
                                foreach (var fieldInfo in tempFieldInfos)
                                {
                                    FieldList.Add(fieldInfo);
                                    GroupFieldList.Add(fieldInfo);
                                }

                                // 默认选择"不分组"选项
                                SelectedGroupField = "(不分组)";
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"加载字段时出错: {ex.Message}", "错误");
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 更新所选要素数量的提示
        /// </summary>
        private void UpdateSelectionInfo()
        {
            Task.Run(async () =>
            {
                int count = 0;
                await QueuedTask.Run(() =>
                {
                    if (_selectedLayer != null)
                        count = _selectedLayer.SelectionCount;
                });

                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (count > 0)
                    {
                        if (!HasSelection && !_useSelection)
                            UseSelection = true; // 新出现选择时默认开启
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
            UpdateSelectionInfo();
        }

        /// <summary>
        /// 清理事件订阅
        /// </summary>
        public void Cleanup()
        {
            if (_mapSelectionChangedToken != null)
            {
                MapSelectionChangedEvent.Unsubscribe(_mapSelectionChangedToken);
                _mapSelectionChangedToken = null;
            }
        }

        /// <summary>
        /// 将数字转换为中文数字
        /// </summary>
        private string ConvertToChinese(int number)
        {
            if (number <= 0)
                return string.Empty;
                
            string[] chineseNumbers = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            
            if (number < 10)
            {
                return chineseNumbers[number];
            }
            else if (number < 100)
            {
                int tens = number / 10;
                int ones = number % 10;
                
                if (tens == 1)
                {
                    return ones == 0 ? "十" : "十" + chineseNumbers[ones];
                }
                else
                {
                    return chineseNumbers[tens] + "十" + (ones == 0 ? "" : chineseNumbers[ones]);
                }
            }
            else
            {
                // 大于100的数字处理
                int hundreds = number / 100;
                int tensAndOnes = number % 100;
                int tens = tensAndOnes / 10;
                int ones = tensAndOnes % 10;
                
                string result = chineseNumbers[hundreds] + "百";
                
                if (tens == 0 && ones == 0)
                {
                    return result;
                }
                else if (tens == 0)
                {
                    return result + "零" + chineseNumbers[ones];
                }
                else
                {
                    if (tens == 1)
                    {
                        return result + (ones == 0 ? "一十" : "一十" + chineseNumbers[ones]);
                    }
                    else
                    {
                        return result + chineseNumbers[tens] + "十" + (ones == 0 ? "" : chineseNumbers[ones]);
                    }
                }
            }
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
                ProgressDialog progressDialog = new ProgressDialog("正在执行中文编号...");
                progressDialog.Show();

                // 在后台线程执行编号操作
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        // 创建编辑操作
                        var editOperation = new EditOperation();
                        editOperation.Name = "地块中文编号";
                        
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
                        bool useSelection = _useSelection && _selectedLayer.SelectionCount > 0;
                        using (var rowCursor = useSelection
                            ? _selectedLayer.GetSelection().Search(queryFilter, false)
                            : featureTable.Search(queryFilter, false))
                        {
                            string groupFieldName = null;
                            // 检查是否有分组字段并且不是"不分组"选项
                            if (_selectedGroupField is FieldInfo fieldInfo)
                            {
                                groupFieldName = fieldInfo.Name;
                            }
                                                 
                            if (!string.IsNullOrEmpty(groupFieldName))
                            {
                                // 按分组编号
                                // 获取分组字段值和对应的要素OID
                                var groupValues = new Dictionary<string, List<long>>();
                                
                                while (rowCursor.MoveNext())
                                {
                                    using (var row = rowCursor.Current)
                                    {
                                        // 获取分组字段值
                                        string groupValue = "DefaultGroup"; // 默认组
                                        
                                        var groupValueObj = row[groupFieldName];
                                        groupValue = groupValueObj?.ToString() ?? "DefaultGroup";
                                        
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
                                        // 转换为中文数字
                                        string chineseNumber = ConvertToChinese(currentNumber);
                                        
                                        // 构建最终编号
                                        string finalNumber = $"{_prefix}{chineseNumber}{_suffix}";
                                        
                                        // 使用EditOperation来更新字段值
                                        editOperation.Modify(featureTable, objectId, 
                                            new Dictionary<string, object> { { _selectedNumberField.Name, finalNumber } });
                                        
                                        currentNumber++;
                                    }
                                }
                            }
                            else
                            {
                                // 不分组编号，按原方式处理
                                // 获取所有符合条件的要素OID
                                var objectIds = new List<long>();
                                
                                while (rowCursor.MoveNext())
                                {
                                    using (var row = rowCursor.Current)
                                    {
                                        objectIds.Add(row.GetObjectID());
                                    }
                                }
                                
                                // 为每个要素编号
                                for (int i = 0; i < objectIds.Count; i++)
                                {
                                    // 计算当前号码
                                    int currentNumber = _startNumber + i;
                                    
                                    // 转换为中文数字
                                    string chineseNumber = ConvertToChinese(currentNumber);
                                    
                                    // 构建最终编号
                                    string finalNumber = $"{_prefix}{chineseNumber}{_suffix}";
                                    
                                    // 使用EditOperation来更新字段值
                                    editOperation.Modify(featureTable, objectIds[i], 
                                        new Dictionary<string, object> { { _selectedNumberField.Name, finalNumber } });
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
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("执行中文编号时出错: " + ex.Message, "错误");
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
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("地块中文编号操作已完成！", "完成");
                
                // 关闭窗口
                CloseWindow();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("执行中文编号时出错: " + ex.Message, "错误");
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
                if (window.Title == "地块中文编号")
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
            string helpContent = "地块中文编号工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于为地块要素添加中文序号编号（一、二、三、四...）。\n\n" +
                               "参数说明：\n" +
                               "1. 编号图层：选择要进行编号的图层\n" +
                               "2. 分组字段：用于对要素进行分组编号的字段，选择\"不分组\"则不分组\n" +
                               "3. 编号字段：用于存储生成的中文编号的字段\n" +
                               "4. 起始号码：编号的起始值，例如设置为5，则编号从\"五\"开始\n" +
                               "5. 前缀：编号前的文本，例如\"编号\"\n" +
                               "6. 后缀：编号后的文本，例如\"号\"\n" +
                               "7. 只编辑空记录：选中时只对编号字段为空的要素进行编号\n\n" +
                               "操作步骤：\n" +
                               "1. 选择需要编号的图层\n" +
                               "2. 选择分组字段（可选）\n" +
                               "3. 选择编号字段\n" +
                               "4. 设置编号参数（起始号码、前后缀等）\n" +
                               "5. 点击执行按钮进行编号\n\n" +
                               "注意事项：\n" +
                               "- 编号字段必须是文本类型\n" +
                               "- 数字越大，中文表示会越长\n" +
                               "- 操作不可恢复，请确认后再执行";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "地块中文编号工具使用说明");
        }
    }
} 