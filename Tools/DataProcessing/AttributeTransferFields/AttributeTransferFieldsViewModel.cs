using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Tools.AttributeTransferFields
{
    /// <summary>
    /// 字段信息（用于下拉与显示）
    /// </summary>
    public class FieldInfo : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; set; }
        public string Alias { get; set; }
        public FieldType FieldType { get; set; }
        public int Length { get; set; }
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public string DisplayName => string.IsNullOrEmpty(Alias) || Name == Alias ? Name : $"{Name}({Alias})";
        public override string ToString() => DisplayName;
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 数据集信息（要素图层或独立表）
    /// </summary>
    public class DatasetInfo
    {
        public FeatureLayer FeatureLayer { get; set; }
        public StandaloneTable StandaloneTable { get; set; }
        public string DisplayName { get; set; }
        public string Name
        {
            get
            {
                if (FeatureLayer != null) return FeatureLayer.Name;
                if (StandaloneTable != null) return StandaloneTable.Name;
                return string.Empty;
            }
        }
        public Table GetTable()
        {
            if (FeatureLayer != null) return FeatureLayer.GetTable();
            if (StandaloneTable != null) return StandaloneTable.GetTable();
            return null;
        }
    }

    /// <summary>
    /// 字段映射项（主字段 -> 从字段）
    /// </summary>
    public class FieldMappingItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _sourceFieldName;
        private string _targetFieldName;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public string SourceFieldName { get => _sourceFieldName; set { _sourceFieldName = value; OnPropertyChanged(); } }
        public string TargetFieldName { get => _targetFieldName; set { _targetFieldName = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// RelayCommand
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute?.Invoke();
        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 属性传递（字段）视图模型
    /// </summary>
    public class AttributeTransferFieldsViewModel : PropertyChangedBase
    {
        #region 私有字段
        private ObservableCollection<DatasetInfo> _primaryList;
        private ObservableCollection<DatasetInfo> _secondaryList;
        private ObservableCollection<FieldInfo> _primaryFieldList;
        private ObservableCollection<FieldInfo> _secondaryFieldList;
        private ObservableCollection<FieldMappingItem> _fieldMappings;
        private DatasetInfo _selectedPrimary;
        private DatasetInfo _selectedSecondary;
        private FieldInfo _selectedPrimaryKeyField;
        private FieldInfo _selectedSecondaryKeyField;
        private bool _onlyFillEmpty = true;
        private bool _isProcessing;
        private string _logText;
        private bool _isPrimaryToSecondary = true;
        private CancellationTokenSource _cts;
        #endregion

        #region 公共属性
        public ObservableCollection<DatasetInfo> PrimaryList { get => _primaryList; set => SetProperty(ref _primaryList, value); }
        public ObservableCollection<DatasetInfo> SecondaryList { get => _secondaryList; set => SetProperty(ref _secondaryList, value); }
        public ObservableCollection<FieldInfo> PrimaryFieldList { get => _primaryFieldList; set => SetProperty(ref _primaryFieldList, value); }
        public ObservableCollection<FieldInfo> SecondaryFieldList { get => _secondaryFieldList; set => SetProperty(ref _secondaryFieldList, value); }
        public ObservableCollection<FieldMappingItem> FieldMappings { get => _fieldMappings; set => SetProperty(ref _fieldMappings, value); }

        public DatasetInfo SelectedPrimary
        {
            get => _selectedPrimary;
            set
            {
                if (SetProperty(ref _selectedPrimary, value))
                {
                    LoadPrimaryFields();
                    RaiseAllCanExecutes();
                }
            }
        }
        public DatasetInfo SelectedSecondary
        {
            get => _selectedSecondary;
            set
            {
                if (SetProperty(ref _selectedSecondary, value))
                {
                    LoadSecondaryFields();
                    RaiseAllCanExecutes();
                }
            }
        }
        public FieldInfo SelectedPrimaryKeyField { get => _selectedPrimaryKeyField; set { if (SetProperty(ref _selectedPrimaryKeyField, value)) RaiseAllCanExecutes(); } }
        public FieldInfo SelectedSecondaryKeyField { get => _selectedSecondaryKeyField; set { if (SetProperty(ref _selectedSecondaryKeyField, value)) RaiseAllCanExecutes(); } }
        public bool OnlyFillEmpty { get => _onlyFillEmpty; set => SetProperty(ref _onlyFillEmpty, value); }

        public bool IsProcessing { get => _isProcessing; set { SetProperty(ref _isProcessing, value); RaiseAllCanExecutes(); } }
        public string LogText { get => _logText; set => SetProperty(ref _logText, value); }

        // 方向：主->从 与 从->主
        public bool IsPrimaryToSecondary
        {
            get => _isPrimaryToSecondary;
            set
            {
                if (SetProperty(ref _isPrimaryToSecondary, value))
                {
                    // 确保互斥
                    NotifyPropertyChanged(() => IsSecondaryToPrimary);
                }
            }
        }
        public bool IsSecondaryToPrimary
        {
            get => !_isPrimaryToSecondary;
            set { IsPrimaryToSecondary = !value; }
        }
        #endregion

        #region 命令
        public RelayCommand RefreshDatasetsCommand { get; private set; }
        public RelayCommand AutoMapCommand { get; private set; }
        public RelayCommand AddMappingCommand { get; private set; }
        public RelayCommand RemoveSelectedMappingsCommand { get; private set; }
        public RelayCommand ClearMappingsCommand { get; private set; }
        public RelayCommand StartCommand { get; private set; }
        public RelayCommand StopCommand { get; private set; }
        public RelayCommand ShowHelpCommand { get; private set; }
        public RelayCommand ShowLogCommand { get; private set; }
        #endregion

        #region 构造函数
        public AttributeTransferFieldsViewModel()
        {
            Initialize();
            InitializeCommands();
            LoadDatasets();
        }
        #endregion

        #region 初始化
        private void Initialize()
        {
            _primaryList = new ObservableCollection<DatasetInfo>();
            _secondaryList = new ObservableCollection<DatasetInfo>();
            _primaryFieldList = new ObservableCollection<FieldInfo>();
            _secondaryFieldList = new ObservableCollection<FieldInfo>();
            _fieldMappings = new ObservableCollection<FieldMappingItem>();
            _fieldMappings.CollectionChanged += FieldMappings_CollectionChanged;
            _isProcessing = false;
            _logText = string.Empty;
        }
        private void FieldMappings_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var it in e.NewItems)
                {
                    if (it is FieldMappingItem fm)
                        fm.PropertyChanged += MappingItem_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (var it in e.OldItems)
                {
                    if (it is FieldMappingItem fm)
                        fm.PropertyChanged -= MappingItem_PropertyChanged;
                }
            }
            RaiseAllCanExecutes();
        }
        private void MappingItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FieldMappingItem.SourceFieldName) ||
                e.PropertyName == nameof(FieldMappingItem.TargetFieldName) ||
                e.PropertyName == nameof(FieldMappingItem.IsSelected))
            {
                RaiseAllCanExecutes();
            }
        }
        private void InitializeCommands()
        {
            RefreshDatasetsCommand = new RelayCommand(() => LoadDatasets(), () => !IsProcessing);
            AutoMapCommand = new RelayCommand(() => AutoMap(), () => CanAutoMap());
            AddMappingCommand = new RelayCommand(() => AddMapping(), () => !IsProcessing);
            RemoveSelectedMappingsCommand = new RelayCommand(() => RemoveSelectedMappings(), () => FieldMappings.Any() && !IsProcessing);
            ClearMappingsCommand = new RelayCommand(() => ClearMappings(), () => FieldMappings.Any() && !IsProcessing);
            StartCommand = new RelayCommand(() => StartTransfer(), () => CanStart());
            StopCommand = new RelayCommand(() => StopTransfer(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
            ShowLogCommand = new RelayCommand(() => ShowLog());
        }
        private void RaiseAllCanExecutes()
        {
            RefreshDatasetsCommand?.RaiseCanExecuteChanged();
            AutoMapCommand?.RaiseCanExecuteChanged();
            AddMappingCommand?.RaiseCanExecuteChanged();
            RemoveSelectedMappingsCommand?.RaiseCanExecuteChanged();
            ClearMappingsCommand?.RaiseCanExecuteChanged();
            StartCommand?.RaiseCanExecuteChanged();
            StopCommand?.RaiseCanExecuteChanged();
        }
        #endregion

        #region 加载图层/表 与 字段
        private async void LoadDatasets()
        {
            LogInfo("正在加载图层/表...");
            await QueuedTask.Run(() =>
            {
                try
                {
                    var map = MapView.Active?.Map;
                    if (map == null) return;

                    var flayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                    var tables = map.GetStandaloneTablesAsFlattenedList().ToList();
                    LogInfo($"图层数: {flayers.Count}，独立表数: {tables.Count}");

                    // 在 MCT 线程过滤，确保仅保留可获取 Table 的数据集（支持 FeatureLayer.GetFeatureClass 兜底）
                    var valid = new List<DatasetInfo>();
                    foreach (var fl in flayers)
                    {
                        var tmp = new DatasetInfo { FeatureLayer = fl, DisplayName = fl?.Name };
                        using (var tbl = TryGetTable(tmp))
                        {
                            if (tbl != null) valid.Add(tmp);
                        }
                    LogInfo($"可用数据源: {valid.Count}");
                    }
                    foreach (var t in tables)
                    {
                        var tmp = new DatasetInfo { StandaloneTable = t, DisplayName = t?.Name };
                        using (var tbl = TryGetTable(tmp))
                        {
                            if (tbl != null) valid.Add(tmp);
                        }
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 重置选择与字段列表，避免旧选择触发字段加载
                        SelectedPrimary = null;
                        SelectedSecondary = null;
                        PrimaryFieldList.Clear();
                        SecondaryFieldList.Clear();
                        PrimaryList.Clear();
                        SecondaryList.Clear();
                        foreach (var ds in valid)
                        {
                            // 分别为主/从构造独立实例，避免共享同一引用造成潜在绑定混淆
                            PrimaryList.Add(new DatasetInfo { FeatureLayer = ds.FeatureLayer, StandaloneTable = ds.StandaloneTable, DisplayName = ds.DisplayName });
                            SecondaryList.Add(new DatasetInfo { FeatureLayer = ds.FeatureLayer, StandaloneTable = ds.StandaloneTable, DisplayName = ds.DisplayName });
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogError($"加载图层/表时出错: {ex.Message}");
                }
            });
        }
        private async void LoadPrimaryFields()
        {
            await LoadFieldsInternal(SelectedPrimary, PrimaryFieldList, sideName: "主");
        }
        private async void LoadSecondaryFields()
        {
            await LoadFieldsInternal(SelectedSecondary, SecondaryFieldList, sideName: "从");
        }
        private async Task LoadFieldsInternal(DatasetInfo ds, ObservableCollection<FieldInfo> target, string sideName)
        {
            if (ds == null)
            {
                target.Clear();
                RaiseAllCanExecutes();
                return;
            }
            await QueuedTask.Run(() =>
            {
                try
                {
                    var tbl = TryGetTable(ds);
                    if (tbl == null)
                    {
                        LogError($"加载{sideName}字段失败：无法获取数据表（{GetDsLabel(ds)}）。");
                        System.Windows.Application.Current.Dispatcher.Invoke(() => { target.Clear(); RaiseAllCanExecutes(); });
                        return;
                    }
                    using (tbl)
                    {
                        var def = tbl.GetDefinition();
                        if (def == null)
                        {
                            LogError($"加载{sideName}字段失败：无法获取表定义。");
                            System.Windows.Application.Current.Dispatcher.Invoke(() => { target.Clear(); RaiseAllCanExecutes(); });
                            return;
                        }
                        var fields = def.GetFields();
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            target.Clear();
                            foreach (var f in fields)
                            {
                                // 排除不适合映射的字段
                                if (f.FieldType == FieldType.Geometry || f.FieldType == FieldType.OID || f.FieldType == FieldType.GlobalID)
                                    continue;
                                if (IsSystemOrNonWritable(f.Name))
                                    continue;
                                target.Add(new FieldInfo
                                {
                                    Name = f.Name,
                                    Alias = f.AliasName,
                                    FieldType = f.FieldType,
                                    Length = f.Length
                                });
                            }
                            RaiseAllCanExecutes();
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogError($"加载{sideName}字段时出错: {ex.Message}");
                    System.Windows.Application.Current.Dispatcher.Invoke(() => { target.Clear(); RaiseAllCanExecutes(); });
                }
            });
        }
        #endregion

        #region 映射操作
        private bool CanAutoMap()
        {
            return !IsProcessing && PrimaryFieldList.Any() && SecondaryFieldList.Any();
        }
        private void AutoMap()
        {
            int before = FieldMappings.Count;
            var priNames = PrimaryFieldList.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var secNames = SecondaryFieldList.Select(f => f.Name).ToList();
            foreach (var name in secNames)
            {
                if (priNames.Contains(name) && !FieldMappings.Any(m => m.SourceFieldName?.Equals(name, StringComparison.OrdinalIgnoreCase) == true || m.TargetFieldName?.Equals(name, StringComparison.OrdinalIgnoreCase) == true))
                {
                    FieldMappings.Add(new FieldMappingItem { IsSelected = true, SourceFieldName = name, TargetFieldName = name });
                }
            }
            LogInfo($"自动匹配完成，共新增 {FieldMappings.Count - before} 条映射。");
            RaiseAllCanExecutes();
        }
        private void AddMapping()
        {
            FieldMappings.Add(new FieldMappingItem { IsSelected = true });
        }
        private void RemoveSelectedMappings()
        {
            var toRemove = FieldMappings.Where(m => m.IsSelected).ToList();
            foreach (var m in toRemove) FieldMappings.Remove(m);
        }
        private void ClearMappings()
        {
            FieldMappings.Clear();
        }
        #endregion

        #region 执行与取消
        private bool CanStart()
        {
            return !IsProcessing && SelectedPrimary != null && SelectedSecondary != null &&
                   SelectedPrimaryKeyField != null && SelectedSecondaryKeyField != null &&
                   FieldMappings.Any(m => !string.IsNullOrWhiteSpace(m.SourceFieldName) && !string.IsNullOrWhiteSpace(m.TargetFieldName));
        }
        private async void StartTransfer()
        {
            LogText = string.Empty;
            LogInfo("开始执行属性传递...");
            IsProcessing = true;
            _cts = new CancellationTokenSource();

            try
            {
                var dirP2S = IsPrimaryToSecondary;
                var keySrc = dirP2S ? SelectedPrimaryKeyField?.Name : SelectedSecondaryKeyField?.Name;
                var keyTgt = dirP2S ? SelectedSecondaryKeyField?.Name : SelectedPrimaryKeyField?.Name;

                var srcDS = dirP2S ? SelectedPrimary : SelectedSecondary;
                var tgtDS = dirP2S ? SelectedSecondary : SelectedPrimary;

                // 有效映射集合
                var effectiveMappings = FieldMappings
                    .Where(m => !string.IsNullOrWhiteSpace(m.SourceFieldName) && !string.IsNullOrWhiteSpace(m.TargetFieldName))
                    .ToList();

                await QueuedTask.Run(async () =>
                {
                    try
                    {
                        var srcTable = TryGetTable(srcDS);
                        if (srcTable == null)
                        {
                            LogError("源数据表获取失败。");
                            return;
                        }
                        var tgtTable = TryGetTable(tgtDS);
                        if (tgtTable == null)
                        {
                            LogError("目标数据表获取失败。");
                            srcTable.Dispose();
                            return;
                        }
                        using (srcTable)
                        using (tgtTable)
                        {
                            // 读取源：key -> 字段值字典
                            var neededSrcFields = new HashSet<string>(effectiveMappings.Select(m => m.SourceFieldName), StringComparer.OrdinalIgnoreCase);
                            neededSrcFields.Add(keySrc);

                            var srcDict = new Dictionary<string, Dictionary<string, object>>();
                            var dupKeys = new HashSet<string>();
                            using (var cursor = srcTable.Search(new QueryFilter(), false))
                            {
                                while (cursor.MoveNext())
                                {
                                    if (_cts.IsCancellationRequested) return;
                                    using (var row = cursor.Current)
                                    {
                                        var k = NormalizeKey(row[keySrc]);
                                        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                                        foreach (var fname in neededSrcFields)
                                        {
                                            try { values[fname] = row[fname]; } catch { values[fname] = null; }
                                        }
                                        if (srcDict.ContainsKey(k)) dupKeys.Add(k);
                                        else srcDict[k] = values;
                                    }
                                }
                            }
                            if (dupKeys.Count > 0)
                                LogWarning($"源数据中存在 {dupKeys.Count} 个重复键，将使用首次出现的记录。");

                            // 执行编辑
                            var op = new EditOperation { Name = "属性传递[字段]" };
                            int updateCount = 0, missCount = 0;

                            using (var cursor = tgtTable.Search(new QueryFilter(), false))
                            {
                                while (cursor.MoveNext())
                                {
                                    if (_cts.IsCancellationRequested) break;
                                    using (var row = cursor.Current)
                                    {
                                        var k = NormalizeKey(row[keyTgt]);
                                        if (!srcDict.TryGetValue(k, out var srcVals)) { missCount++; continue; }

                                        var updates = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                                        foreach (var map in effectiveMappings)
                                        {
                                            var srcName = map.SourceFieldName;
                                            var tgtName = map.TargetFieldName;

                                            // 按当前方向确定最终源/目标字段
                                            string finalSource = dirP2S ? srcName : tgtName;
                                            string finalTarget = dirP2S ? tgtName : srcName;

                                            object v = null;
                                            srcVals?.TryGetValue(finalSource, out v);

                                            // 简单类型兼容检查，不兼容则跳过
                                            if (!IsTypeCompatible(srcTable, tgtTable, finalSource, finalTarget))
                                            {
                                                LogWarning($"字段类型不兼容，已跳过: {finalSource} -> {finalTarget}");
                                                continue;
                                            }

                                            // 只填空值：当目标已有值时跳过
                                            if (OnlyFillEmpty)
                                            {
                                                object curr = null;
                                                try { curr = row[finalTarget]; } catch { curr = null; }
                                                if (!IsNullOrEmptyValue(curr))
                                                {
                                                    continue;
                                                }
                                            }

                                            updates[finalTarget] = v;
                                        }

                                        if (updates.Count > 0)
                                        {
                                            op.Modify(tgtTable, row.GetObjectID(), updates);
                                            updateCount++;
                                        }
                                    }
                                }
                            }

                            bool ok = op.Execute();
                            if (!ok)
                            {
                                throw new Exception("编辑操作执行失败");
                            }
                            LogInfo($"属性传递完成：更新 {updateCount} 条；未匹配 {missCount} 条。");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"执行属性传递时出错: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"执行时出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                _cts = null;
            }
        }
        private void StopTransfer()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                LogWarning("已请求停止操作...");
            }
        }
        #endregion

        #region 帮助
        private void ShowHelp()
        {
            string help =
                "属性传递[字段] 使用说明\n\n" +
                "功能：\n" +
                "在主/从两个数据集之间，根据关联键字段进行记录匹配，并按字段映射批量传递属性值。\n\n" +
                "步骤：\n" +
                "1) 选择主数据集与从数据集（支持要素图层与独立表）。\n" +
                "2) 分别选择主键字段与从键字段，用于匹配记录。\n" +
                "3) 通过自动匹配或手动添加，建立‘主字段 → 从字段’映射列表。\n" +
                "4) 选择传递方向（主→从 或 从→主）。\n" +
                "5) 点击开始执行，查看日志输出。\n\n" +
                "说明：\n" +
                "- 键值大小写与前后空格会被标准化再匹配。\n" +
                "- 数值类型之间可互转，任意类型可传到字符串字段。\n" +
                "- 若源数据存在重复键，将以首次出现的记录为准并给出警告。\n" +
                "- 可随时点击停止以取消正在进行的操作。";

            MessageBox.Show(help, "属性传递[字段] 使用说明");
        }

        private void ShowLog()
        {
            var text = string.IsNullOrWhiteSpace(LogText) ? "暂无日志" : LogText;
            MessageBox.Show(text, "执行日志");
        }
        #endregion

        #region 辅助
        private static readonly HashSet<string> _blockedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OBJECTID","FID","OID","GLOBALID","SHAPE","SHAPE_LENGTH","SHAPE_AREA",
            "CREATED_USER","CREATED_DATE","LAST_EDITED_USER","LAST_EDITED_DATE",
            "CREATOR","EDITOR","EDITDATE","CREATIONDATE","LASTUPDATE",
        };
        private static bool IsSystemOrNonWritable(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            return _blockedNames.Contains(name);
        }
        private static bool IsNullOrEmptyValue(object v)
        {
            if (v == null || v is DBNull) return true;
            switch (v)
            {
                case string s:
                    return string.IsNullOrWhiteSpace(s);
                case short or int or long or float or double or decimal:
                    // 数值型：认为 null 才是空，0 不是空
                    return false;
                case bool b:
                    // 布尔：null 才是空
                    return false;
                case DateTime dt:
                    // 1900-01-01 或 MinValue 视为“空”常用占位
                    return dt == DateTime.MinValue || dt.Year <= 1900;
                case Guid g:
                    return g == Guid.Empty;
                default:
                    return false;
            }
        }
        private static string GetDsLabel(DatasetInfo ds)
        {
            if (ds == null) return "<null>";
            var type = ds.FeatureLayer != null ? "要素图层" : (ds.StandaloneTable != null ? "独立表" : "未知");
            return $"{ds.DisplayName ?? ds.Name} | {type}";
        }
        private static Table TryGetTable(DatasetInfo ds)
        {
            try
            {
                if (ds == null) return null;
                if (ds.FeatureLayer != null)
                {
                    var tbl = ds.FeatureLayer.GetTable();
                    if (tbl != null) return tbl;
                    // 兜底：直接获取 FeatureClass
                    var fc = ds.FeatureLayer.GetFeatureClass();
                    return fc; // FeatureClass 继承自 Table
                }
                if (ds.StandaloneTable != null)
                {
                    return ds.StandaloneTable.GetTable();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        private static string NormalizeKey(object v)
        {
            if (v == null || v is DBNull) return string.Empty;
            return v.ToString()?.Trim()?.ToUpperInvariant() ?? string.Empty;
        }
        private bool IsTypeCompatible(Table src, Table tgt, string srcField, string tgtField)
        {
            try
            {
                var sdef = src.GetDefinition();
                var tdef = tgt.GetDefinition();
                var sf = sdef.GetFields().FirstOrDefault(f => f.Name.Equals(srcField, StringComparison.OrdinalIgnoreCase));
                var tf = tdef.GetFields().FirstOrDefault(f => f.Name.Equals(tgtField, StringComparison.OrdinalIgnoreCase));
                if (sf == null || tf == null) return false;

                if (sf.FieldType == tf.FieldType) return true;
                // 允许数字之间互转
                bool sNum = IsNumericType(sf.FieldType), tNum = IsNumericType(tf.FieldType);
                if (sNum && tNum) return true;
                // 允许任意到字符串
                if (tf.FieldType == FieldType.String) return true;
                return false;
            }
            catch { return false; }
        }
        private bool IsNumericType(FieldType t)
        {
            return t == FieldType.Integer || t == FieldType.SmallInteger || t == FieldType.Double || t == FieldType.Single;
        }
        private void LogInfo(string msg) => AppendLog($"[信息] {msg}");
        private void LogWarning(string msg) => AppendLog($"[警告] {msg}");
        private void LogError(string msg) => AppendLog($"[错误] {msg}");
        private void AppendLog(string msg)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                LogText += (string.IsNullOrEmpty(LogText) ? string.Empty : "\n") + msg;
            });
        }
        #endregion
    }
}
