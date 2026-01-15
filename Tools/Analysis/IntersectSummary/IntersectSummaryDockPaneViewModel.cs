using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Tools.IntersectSummary
{
    /// <summary>
    /// 字段选择项
    /// </summary>
    public class FieldSelectItem : PropertyChangedBase
    {
        public string FieldName { get; set; }
        public string Alias { get; set; }
        public string FieldType { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string DisplayText => string.IsNullOrEmpty(Alias) || Alias == FieldName
            ? $"{FieldName}（{FieldType}）"
            : $"{FieldName}（{Alias}）（{FieldType}）";

        public override string ToString() => DisplayText;
    }

    /// <summary>
    /// 交集汇总结果项
    /// </summary>
    public class IntersectSummaryResultItem
    {
        public Dictionary<string, object> RegionValues { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> ClassValues { get; set; } = new Dictionary<string, object>();
        public double Area { get; set; }
        public double AdjustedArea { get; set; }
    }

    /// <summary>
    /// 交集汇总表DockPane视图模型
    /// </summary>
    internal class IntersectSummaryDockPaneViewModel : PropertyChangedBase
    {
        #region 属性

        // 取消操作标志
        private bool _cancelRequested = false;
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
        }

        // 是否正在处理
        private bool _isProcessing = false;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        // 是否可以处理（区域字段可选，类字段必选）
        public bool CanProcess => !IsProcessing && 
                                  SelectedRedlineLayer != null && 
                                  SelectedClassLayer != null &&
                                  ClassFields?.Any(f => f.IsSelected) == true;

        // 面图层列表
        private ObservableCollection<FeatureLayer> _polygonLayers;
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }

        // 选中的红线图层
        private FeatureLayer _selectedRedlineLayer;
        public FeatureLayer SelectedRedlineLayer
        {
            get => _selectedRedlineLayer;
            set
            {
                SetProperty(ref _selectedRedlineLayer, value);
                NotifyPropertyChanged(() => HasSelectedRedlineLayer);
                NotifyPropertyChanged(() => CanProcess);
                LoadRegionFields();
            }
        }

        // 是否有选中红线图层
        public bool HasSelectedRedlineLayer => SelectedRedlineLayer != null;

        // 选中的类要素图层
        private FeatureLayer _selectedClassLayer;
        public FeatureLayer SelectedClassLayer
        {
            get => _selectedClassLayer;
            set
            {
                SetProperty(ref _selectedClassLayer, value);
                NotifyPropertyChanged(() => HasSelectedClassLayer);
                NotifyPropertyChanged(() => CanProcess);
                LoadClassFields();
            }
        }

        // 是否有选中类要素图层
        public bool HasSelectedClassLayer => SelectedClassLayer != null;

        // 区域字段列表（可多选）
        private ObservableCollection<FieldSelectItem> _regionFields;
        public ObservableCollection<FieldSelectItem> RegionFields
        {
            get => _regionFields;
            set => SetProperty(ref _regionFields, value);
        }

        // 类字段列表（可多选）
        private ObservableCollection<FieldSelectItem> _classFields;
        public ObservableCollection<FieldSelectItem> ClassFields
        {
            get => _classFields;
            set => SetProperty(ref _classFields, value);
        }

        // 面积单位列表
        private ObservableCollection<string> _areaUnits;
        public ObservableCollection<string> AreaUnits
        {
            get => _areaUnits;
            set => SetProperty(ref _areaUnits, value);
        }

        // 选中的面积单位
        private string _selectedAreaUnit;
        public string SelectedAreaUnit
        {
            get => _selectedAreaUnit;
            set
            {
                SetProperty(ref _selectedAreaUnit, value);
                UpdateDefaultDecimalPlaces();
            }
        }

        // 保留小数位数
        private int _decimalPlaces = 2;
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set => SetProperty(ref _decimalPlaces, value);
        }

        // 进度值
        private int _progress = 0;
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        // 进度条是否不确定
        private bool _isProgressIndeterminate = false;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        // 状态消息
        private string _statusMessage = "请选择图层和字段。";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // 日志内容
        private string _logContent = "";
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        // 结果数据表
        private DataTable _resultTable;
        public DataTable ResultTable
        {
            get => _resultTable;
            set => SetProperty(ref _resultTable, value);
        }

        // 当前结果的区域字段数量（用于导出时合并单元格）
        private int _regionFieldCount = 0;

        // 是否有结果
        public bool HasResult => ResultTable != null && ResultTable.Rows.Count > 0;

        #endregion

        #region 命令

        // 运行命令
        private ICommand _runCommand;
        public ICommand RunCommand
        {
            get
            {
                return _runCommand ?? (_runCommand = new RelayCommand(async () => await ExecuteAsync(), () => CanProcess));
            }
        }

        // 取消命令
        private ICommand _cancelCommand;
        public ICommand CancelCommand
        {
            get
            {
                return _cancelCommand ?? (_cancelCommand = new RelayCommand(() => Cancel(), () => IsProcessing));
            }
        }

        // 显示帮助命令
        private ICommand _showHelpCommand;
        public ICommand ShowHelpCommand
        {
            get
            {
                return _showHelpCommand ?? (_showHelpCommand = new RelayCommand(() => ShowHelp()));
            }
        }

        // 刷新图层命令
        private ICommand _refreshLayersCommand;
        public ICommand RefreshLayersCommand
        {
            get
            {
                return _refreshLayersCommand ?? (_refreshLayersCommand = new RelayCommand(() => RefreshLayers()));
            }
        }

        // 导出命令
        private ICommand _exportCommand;
        public ICommand ExportCommand
        {
            get
            {
                return _exportCommand ?? (_exportCommand = new RelayCommand(() => ExportResult(), () => HasResult));
            }
        }

        // 显示结果命令
        private ICommand _showResultCommand;
        public ICommand ShowResultCommand
        {
            get
            {
                return _showResultCommand ?? (_showResultCommand = new RelayCommand(() => ShowResultWindow(), () => HasResult));
            }
        }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public IntersectSummaryDockPaneViewModel()
        {
            // 初始化属性
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            RegionFields = new ObservableCollection<FieldSelectItem>();
            ClassFields = new ObservableCollection<FieldSelectItem>();

            // 初始化面积单位
            AreaUnits = new ObservableCollection<string> { "平方米", "公顷", "亩" };
            SelectedAreaUnit = "平方米";

            StatusMessage = "请选择图层和字段。";
            LogContent = "";
            Progress = 0;
            IsProgressIndeterminate = false;

            // 加载面图层
            LoadPolygonLayers();
        }

        /// <summary>
        /// 根据单位更新默认小数位数
        /// </summary>
        private void UpdateDefaultDecimalPlaces()
        {
            DecimalPlaces = SelectedAreaUnit switch
            {
                "平方米" => 2,
                "公顷" => 4,
                "亩" => 4,
                _ => 2
            };
        }

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            LoadPolygonLayers();
        }

        /// <summary>
        /// 加载面图层
        /// </summary>
        private void LoadPolygonLayers()
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
                                if (layer.GetFeatureClass()?.GetDefinition()?.GetShapeType() == GeometryType.Polygon)
                                {
                                    tempLayers.Add(layer);
                                }
                            }
                        }
                    });

                    // 在UI线程更新图层列表
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            PolygonLayers?.Clear();

                            if (PolygonLayers != null)
                            {
                                foreach (var layer in tempLayers)
                                {
                                    PolygonLayers.Add(layer);
                                }
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
                            StatusMessage = $"加载图层出错: {ex.Message}";
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 加载区域字段
        /// </summary>
        private void LoadRegionFields()
        {
            if (SelectedRedlineLayer == null)
            {
                RegionFields?.Clear();
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var tempFieldInfos = new List<FieldSelectItem>();

                    await QueuedTask.Run(() =>
                    {
                        var featureClass = SelectedRedlineLayer.GetFeatureClass();
                        if (featureClass != null)
                        {
                            var definition = featureClass.GetDefinition();
                            var fields = definition.GetFields();

                            foreach (var field in fields)
                            {
                                // 跳过系统字段
                                if (field.FieldType == FieldType.Geometry ||
                                    field.FieldType == FieldType.OID ||
                                    field.FieldType == FieldType.GlobalID ||
                                    field.FieldType == FieldType.Blob ||
                                    field.FieldType == FieldType.Raster)
                                    continue;

                                var fieldInfo = new FieldSelectItem
                                {
                                    FieldName = field.Name,
                                    Alias = field.AliasName,
                                    FieldType = GetFieldTypeDisplayName(field.FieldType),
                                    IsSelected = false
                                };
                                fieldInfo.PropertyChanged += (s, e) =>
                                {
                                    if (e.PropertyName == nameof(FieldSelectItem.IsSelected))
                                    {
                                        NotifyPropertyChanged(() => CanProcess);
                                    }
                                };
                                tempFieldInfos.Add(fieldInfo);
                            }
                        }
                    });

                    // 在UI线程更新字段列表
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            RegionFields?.Clear();
                            if (RegionFields != null)
                            {
                                foreach (var fieldInfo in tempFieldInfos)
                                {
                                    RegionFields.Add(fieldInfo);
                                }
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
                            StatusMessage = $"加载字段出错: {ex.Message}";
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 加载类字段
        /// </summary>
        private void LoadClassFields()
        {
            if (SelectedClassLayer == null)
            {
                ClassFields?.Clear();
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var tempFieldInfos = new List<FieldSelectItem>();

                    await QueuedTask.Run(() =>
                    {
                        var featureClass = SelectedClassLayer.GetFeatureClass();
                        if (featureClass != null)
                        {
                            var definition = featureClass.GetDefinition();
                            var fields = definition.GetFields();

                            foreach (var field in fields)
                            {
                                // 跳过系统字段
                                if (field.FieldType == FieldType.Geometry ||
                                    field.FieldType == FieldType.OID ||
                                    field.FieldType == FieldType.GlobalID ||
                                    field.FieldType == FieldType.Blob ||
                                    field.FieldType == FieldType.Raster)
                                    continue;

                                var fieldInfo = new FieldSelectItem
                                {
                                    FieldName = field.Name,
                                    Alias = field.AliasName,
                                    FieldType = GetFieldTypeDisplayName(field.FieldType),
                                    IsSelected = false
                                };
                                fieldInfo.PropertyChanged += (s, e) =>
                                {
                                    if (e.PropertyName == nameof(FieldSelectItem.IsSelected))
                                    {
                                        NotifyPropertyChanged(() => CanProcess);
                                    }
                                };
                                tempFieldInfos.Add(fieldInfo);
                            }
                        }
                    });

                    // 在UI线程更新字段列表
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ClassFields?.Clear();
                            if (ClassFields != null)
                            {
                                foreach (var fieldInfo in tempFieldInfos)
                                {
                                    ClassFields.Add(fieldInfo);
                                }
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
                            StatusMessage = $"加载字段出错: {ex.Message}";
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 获取字段类型的显示名称
        /// </summary>
        private string GetFieldTypeDisplayName(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.Double => "双精度",
                FieldType.Single => "单精度",
                FieldType.Integer => "整型",
                FieldType.SmallInteger => "短整型",
                FieldType.String => "文本",
                FieldType.BigInteger => "长整型",
                FieldType.Date => "日期",
                _ => "其他"
            };
        }

        /// <summary>
        /// 执行交集汇总计算
        /// </summary>
        private async Task ExecuteAsync()
        {
            if (SelectedRedlineLayer == null || SelectedClassLayer == null)
            {
                StatusMessage = "请选择区域图层和类要素图层。";
                return;
            }

            // 区域字段可选，类字段必选
            var selectedRegionFields = RegionFields?.Where(f => f.IsSelected).Select(f => f.FieldName).ToList() ?? new List<string>();
            var selectedClassFields = ClassFields?.Where(f => f.IsSelected).Select(f => f.FieldName).ToList();

            if (selectedClassFields == null || !selectedClassFields.Any())
            {
                StatusMessage = "请至少选择一个类字段。";
                return;
            }

            try
            {
                IsProcessing = true;
                CancelRequested = false;
                Progress = 0;
                IsProgressIndeterminate = true;
                StatusMessage = "正在计算交集汇总...";
                LogContent = "";

                LogInfo($"开始计算交集汇总");
                LogInfo($"红线图层: {SelectedRedlineLayer.Name}");
                LogInfo($"区域字段: {string.Join(", ", selectedRegionFields)}");
                LogInfo($"类要素图层: {SelectedClassLayer.Name}");
                LogInfo($"类字段: {string.Join(", ", selectedClassFields)}");
                LogInfo($"单位: {SelectedAreaUnit}, 小数位数: {DecimalPlaces}");

                var results = new List<IntersectSummaryResultItem>();

                await QueuedTask.Run(() =>
                {
                    try
                    {
                        if (CancelRequested) return;

                        var redlineFC = SelectedRedlineLayer.GetFeatureClass();
                        var classFC = SelectedClassLayer.GetFeatureClass();

                        if (redlineFC == null || classFC == null)
                        {
                            LogError("无法获取要素类");
                            return;
                        }

                        // 获取两个图层的空间参考
                        var redlineSR = redlineFC.GetDefinition().GetSpatialReference();
                        var classSR = classFC.GetDefinition().GetSpatialReference();

                        // 获取红线要素
                        var redlineFeatures = new List<(long OID, Polygon Geometry, Dictionary<string, object> Attributes)>();
                        using (var cursor = redlineFC.Search())
                        {
                            while (cursor.MoveNext())
                            {
                                if (CancelRequested) return;
                                using (var feature = cursor.Current as Feature)
                                {
                                    if (feature?.GetShape() is Polygon polygon)
                                    {
                                        var attrs = new Dictionary<string, object>();
                                        foreach (var fieldName in selectedRegionFields)
                                        {
                                            attrs[fieldName] = feature[fieldName];
                                        }
                                        redlineFeatures.Add((feature.GetObjectID(), polygon, attrs));
                                    }
                                }
                            }
                        }

                        LogInfo($"共有 {redlineFeatures.Count} 个红线要素");

                        // 更新进度条为确定模式
                        UpdateProgress(false, 0);

                        int processedCount = 0;
                        int totalCount = redlineFeatures.Count;

                        // 按红线要素分组存储交集结果，用于面积调平
                        var redlineIntersects = new Dictionary<long, List<IntersectSummaryResultItem>>();

                        foreach (var redline in redlineFeatures)
                        {
                            if (CancelRequested)
                            {
                                LogWarning("操作已取消");
                                return;
                            }

                            // 获取红线要素的实际面积
                            double redlineArea = redline.Geometry.Area;

                            // 如果空间参考不同，需要将红线几何投影到类图层坐标系进行空间查询
                            Polygon queryGeometry = redline.Geometry;
                            if (!SpatialReference.AreEqual(redlineSR, classSR, false))
                            {
                                queryGeometry = GeometryEngine.Instance.Project(redline.Geometry, classSR) as Polygon;
                            }

                            // 使用空间过滤器获取与红线相交的类要素
                            var spatialFilter = new SpatialQueryFilter
                            {
                                FilterGeometry = queryGeometry,
                                SpatialRelationship = SpatialRelationship.Intersects
                            };

                            var intersectItems = new List<IntersectSummaryResultItem>();

                            using (var classCursor = classFC.Search(spatialFilter))
                            {
                                while (classCursor.MoveNext())
                                {
                                    if (CancelRequested) return;

                                    using (var classFeature = classCursor.Current as Feature)
                                    {
                                        if (classFeature?.GetShape() is Polygon classPolygon)
                                        {
                                            // 将类图层几何投影到红线图层坐标系进行交集运算
                                            Polygon projectedClass = classPolygon;
                                            if (!SpatialReference.AreEqual(redlineSR, classSR, false))
                                            {
                                                projectedClass = GeometryEngine.Instance.Project(classPolygon, redlineSR) as Polygon;
                                            }

                                            // 计算交集
                                            var intersection = GeometryEngine.Instance.Intersection(redline.Geometry, projectedClass);
                                            if (intersection != null && !intersection.IsEmpty && intersection is Polygon intersectPolygon)
                                            {
                                                double intersectArea = intersectPolygon.Area;
                                                if (intersectArea > 0.0001) // 忽略极小面积
                                                {
                                                    var resultItem = new IntersectSummaryResultItem
                                                    {
                                                        RegionValues = new Dictionary<string, object>(redline.Attributes),
                                                        Area = intersectArea
                                                    };

                                                    foreach (var fieldName in selectedClassFields)
                                                    {
                                                        resultItem.ClassValues[fieldName] = classFeature[fieldName];
                                                    }

                                                    intersectItems.Add(resultItem);
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // 计算交集面积总和
                            double totalIntersectArea = intersectItems.Sum(i => i.Area);

                            // 计算未覆盖面积（不在类范围内的面积）
                            double uncoveredArea = redlineArea - totalIntersectArea;

                            // 如果存在未覆盖面积，添加"其他"类别
                            if (uncoveredArea > 0.0001) // 容差0.0001平方米
                            {
                                var uncoveredItem = new IntersectSummaryResultItem
                                {
                                    RegionValues = new Dictionary<string, object>(),
                                    ClassValues = new Dictionary<string, object>(),
                                    Area = uncoveredArea,
                                    AdjustedArea = uncoveredArea
                                };

                                // 复制区域字段值
                                foreach (var fieldName in selectedRegionFields)
                                {
                                    uncoveredItem.RegionValues[fieldName] = redline.Attributes.ContainsKey(fieldName) ? redline.Attributes[fieldName] : null;
                                }

                                // 类字段设置为"其他"
                                foreach (var fieldName in selectedClassFields)
                                {
                                    uncoveredItem.ClassValues[fieldName] = "其他";
                                }

                                intersectItems.Add(uncoveredItem);
                                totalIntersectArea += uncoveredArea; // 更新总面积
                            }

                            // 面积调平：确保交集面积之和等于红线要素的实际面积
                            if (intersectItems.Count > 0)
                            {
                                if (totalIntersectArea > 0)
                                {
                                    // 按比例调整各交集面积
                                    double adjustmentRatio = redlineArea / totalIntersectArea;

                                    foreach (var item in intersectItems)
                                    {
                                        item.AdjustedArea = item.Area * adjustmentRatio;
                                    }

                                    // 处理调整后的舍入误差
                                    double adjustedTotal = intersectItems.Sum(i => i.AdjustedArea);
                                    double remainder = redlineArea - adjustedTotal;

                                    // 将剩余误差加到最大面积的项上
                                    if (Math.Abs(remainder) > 0.0000001 && intersectItems.Count > 0)
                                    {
                                        var maxItem = intersectItems.OrderByDescending(i => i.AdjustedArea).First();
                                        maxItem.AdjustedArea += remainder;
                                    }
                                }
                                else
                                {
                                    foreach (var item in intersectItems)
                                    {
                                        item.AdjustedArea = item.Area;
                                    }
                                }

                                results.AddRange(intersectItems);
                            }
                            else
                            {
                                // 如果没有任何交集，整个区域都是"其他"
                                var uncoveredItem = new IntersectSummaryResultItem
                                {
                                    RegionValues = new Dictionary<string, object>(),
                                    ClassValues = new Dictionary<string, object>(),
                                    Area = redlineArea,
                                    AdjustedArea = redlineArea
                                };

                                foreach (var fieldName in selectedRegionFields)
                                {
                                    uncoveredItem.RegionValues[fieldName] = redline.Attributes.ContainsKey(fieldName) ? redline.Attributes[fieldName] : null;
                                }

                                foreach (var fieldName in selectedClassFields)
                                {
                                    uncoveredItem.ClassValues[fieldName] = "其他";
                                }

                                results.Add(uncoveredItem);
                            }

                            processedCount++;
                            int progressPercent = (int)((double)processedCount / totalCount * 100);
                            UpdateProgress(false, progressPercent);
                            UpdateStatus($"正在处理... ({processedCount}/{totalCount})");
                        }

                        LogInfo($"交集计算完成，共 {results.Count} 条记录");
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理过程中出错: {ex.Message}");
                    }
                });

                if (!CancelRequested && results.Count > 0)
                {
                    // 按区域字段和类字段汇总
                    var summaryResults = AggregateResults(results, selectedRegionFields, selectedClassFields);

                    // 创建结果表
                    CreateResultTable(summaryResults, selectedRegionFields, selectedClassFields);

                    LogInfo($"汇总完成，共 {summaryResults.Count} 条汇总记录");
                    StatusMessage = "处理完成！";
                    Progress = 100;
                    NotifyPropertyChanged(() => HasResult);
                }
                else if (CancelRequested)
                {
                    StatusMessage = "操作已取消";
                }
                else
                {
                    StatusMessage = "未找到交集数据";
                }
            }
            catch (Exception ex)
            {
                LogError($"执行出错: {ex.Message}");
                StatusMessage = $"执行出错: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
            }
        }

        /// <summary>
        /// 按区域和类别汇总结果
        /// </summary>
        private List<IntersectSummaryResultItem> AggregateResults(
            List<IntersectSummaryResultItem> results,
            List<string> regionFields,
            List<string> classFields)
        {
            var grouped = results.GroupBy(r =>
            {
                var regionKey = string.Join("|", regionFields.Select(f => r.RegionValues.ContainsKey(f) ? (r.RegionValues[f]?.ToString() ?? "") : ""));
                var classKey = string.Join("|", classFields.Select(f => r.ClassValues.ContainsKey(f) ? (r.ClassValues[f]?.ToString() ?? "") : ""));
                return $"{regionKey}||{classKey}";
            });

            var aggregated = new List<IntersectSummaryResultItem>();

            foreach (var group in grouped)
            {
                var first = group.First();
                var item = new IntersectSummaryResultItem
                {
                    RegionValues = new Dictionary<string, object>(first.RegionValues),
                    ClassValues = new Dictionary<string, object>(first.ClassValues),
                    Area = group.Sum(g => g.Area),
                    AdjustedArea = group.Sum(g => g.AdjustedArea)
                };
                aggregated.Add(item);
            }

            return aggregated;
        }

        /// <summary>
        /// 创建结果表
        /// </summary>
        private void CreateResultTable(
            List<IntersectSummaryResultItem> results,
            List<string> regionFields,
            List<string> classFields)
        {
            var table = new DataTable();

            // 记录区域字段数量（用于导出时合并单元格）
            _regionFieldCount = regionFields.Count;

            // 添加区域字段列
            foreach (var field in regionFields)
            {
                var regionField = RegionFields.FirstOrDefault(f => f.FieldName == field);
                var columnName = regionField != null && !string.IsNullOrEmpty(regionField.Alias) && regionField.Alias != field
                    ? regionField.Alias
                    : field;
                table.Columns.Add(columnName, typeof(string));
            }

            // 添加类字段列
            foreach (var field in classFields)
            {
                var classField = ClassFields.FirstOrDefault(f => f.FieldName == field);
                var columnName = classField != null && !string.IsNullOrEmpty(classField.Alias) && classField.Alias != field
                    ? classField.Alias
                    : field;
                table.Columns.Add(columnName, typeof(string));
            }

            // 添加面积列
            string areaColumnName = $"面积({SelectedAreaUnit})";
            table.Columns.Add(areaColumnName, typeof(double));

            // 使用最大余额法调平面积（按区域分组）
            var adjustedAreas = ApplyLargestRemainderMethod(results, regionFields);

            // 填充数据
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var row = table.NewRow();

                int colIndex = 0;
                foreach (var field in regionFields)
                {
                    row[colIndex++] = result.RegionValues.ContainsKey(field) ? (result.RegionValues[field]?.ToString() ?? "") : "";
                }

                foreach (var field in classFields)
                {
                    row[colIndex++] = result.ClassValues.ContainsKey(field) ? (result.ClassValues[field]?.ToString() ?? "") : "";
                }

                // 使用调平后的面积
                row[colIndex] = adjustedAreas[i];

                table.Rows.Add(row);
            }

            // 添加合计行
            var totalRow = table.NewRow();
            int totalColIndex = 0;
            
            // 区域字段列显示空或"合计"
            if (regionFields.Count > 0)
            {
                totalRow[totalColIndex++] = "合计";
                for (int i = 1; i < regionFields.Count; i++)
                {
                    totalRow[totalColIndex++] = "";
                }
            }
            
            // 类字段列显示空
            for (int i = 0; i < classFields.Count; i++)
            {
                if (regionFields.Count == 0 && i == 0)
                {
                    totalRow[totalColIndex++] = "合计";
                }
                else
                {
                    totalRow[totalColIndex++] = "";
                }
            }
            
            // 面积列显示总和
            double totalArea = adjustedAreas.Sum();
            totalRow[totalColIndex] = Math.Round(totalArea, DecimalPlaces);
            table.Rows.Add(totalRow);

            // 在UI线程更新结果表
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ResultTable = table;
                    NotifyPropertyChanged(() => HasResult);
                });
            }
        }

        /// <summary>
        /// 最大余额法调平面积（确保保留小数位后子面积之和等于总面积）
        /// </summary>
        private List<double> ApplyLargestRemainderMethod(
            List<IntersectSummaryResultItem> results,
            List<string> regionFields)
        {
            var adjustedAreas = new double[results.Count];
            double minUnit = Math.Pow(10, -DecimalPlaces); // 最小单位，如保留2位则为0.01

            // 按区域字段分组
            var groups = results
                .Select((r, index) => new { Result = r, Index = index })
                .GroupBy(x =>
                {
                    var regionKey = string.Join("|", regionFields.Select(f =>
                        x.Result.RegionValues.ContainsKey(f) ? (x.Result.RegionValues[f]?.ToString() ?? "") : ""));
                    return regionKey;
                });

            foreach (var group in groups)
            {
                var items = group.ToList();

                // 转换为目标单位
                var convertedAreas = items.Select(x => ConvertAreaUnit(x.Result.AdjustedArea, SelectedAreaUnit)).ToList();

                // 计算该组的总面积（保留指定小数位）
                double groupTotal = Math.Round(convertedAreas.Sum(), DecimalPlaces);

                // 计算每项的floor值和余额
                var floorValues = convertedAreas.Select(a => Math.Floor(a / minUnit) * minUnit).ToList();
                var remainders = convertedAreas.Select((a, i) => a - floorValues[i]).ToList();

                // 计算floor总和
                double floorTotal = Math.Round(floorValues.Sum(), DecimalPlaces);

                // 需要分配的差额（以最小单位计）
                int unitsToDistribute = (int)Math.Round((groupTotal - floorTotal) / minUnit);

                // 按余额从大到小排序，获取索引
                var sortedByRemainder = items
                    .Select((x, i) => new { ItemIndex = i, Remainder = remainders[i] })
                    .OrderByDescending(x => x.Remainder)
                    .ToList();

                // 分配单位给余额最大的项
                var finalValues = floorValues.ToList();
                for (int i = 0; i < Math.Min(unitsToDistribute, sortedByRemainder.Count); i++)
                {
                    finalValues[sortedByRemainder[i].ItemIndex] += minUnit;
                }

                // 写入结果
                for (int i = 0; i < items.Count; i++)
                {
                    adjustedAreas[items[i].Index] = Math.Round(finalValues[i], DecimalPlaces);
                }
            }

            return adjustedAreas.ToList();
        }

        /// <summary>
        /// 转换面积单位
        /// </summary>
        private double ConvertAreaUnit(double areaInSquareMeters, string targetUnit)
        {
            return targetUnit switch
            {
                "平方米" => areaInSquareMeters,
                "公顷" => areaInSquareMeters / 10000.0,
                "亩" => areaInSquareMeters / 666.6666666667,
                _ => areaInSquareMeters
            };
        }

        /// <summary>
        /// 导出结果
        /// </summary>
        private void ExportResult()
        {
            if (ResultTable == null || ResultTable.Rows.Count == 0)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("没有可导出的数据", "提示");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel文件 (*.xlsx)|*.xlsx|CSV文件 (*.csv)|*.csv",
                DefaultExt = ".xlsx",
                FileName = $"交集汇总表_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = saveDialog.FileName;
                    string extension = Path.GetExtension(filePath).ToLower();

                    if (extension == ".csv")
                    {
                        ExportToCsv(filePath);
                    }
                    else if (extension == ".xlsx")
                    {
                        ExportToExcel(filePath);
                    }

                    LogInfo($"导出成功: {filePath}");
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"导出成功!\n{filePath}", "成功");
                }
                catch (Exception ex)
                {
                    LogError($"导出失败: {ex.Message}");
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"导出失败: {ex.Message}", "错误");
                }
            }
        }

        /// <summary>
        /// 导出为CSV
        /// </summary>
        private void ExportToCsv(string filePath)
        {
            using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                // 写入列标题
                var columnNames = new List<string>();
                foreach (DataColumn col in ResultTable.Columns)
                {
                    columnNames.Add($"\"{col.ColumnName}\"");
                }
                writer.WriteLine(string.Join(",", columnNames));

                // 写入数据行
                foreach (DataRow row in ResultTable.Rows)
                {
                    var values = new List<string>();
                    foreach (var item in row.ItemArray)
                    {
                        if (item is double d)
                        {
                            values.Add(d.ToString($"F{DecimalPlaces}"));
                        }
                        else
                        {
                            values.Add($"\"{item}\"");
                        }
                    }
                    writer.WriteLine(string.Join(",", values));
                }
            }
        }

        /// <summary>
        /// 导出为Excel
        /// </summary>
        private void ExportToExcel(string filePath)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;

            try
            {
                // 创建Excel应用程序
                excelApp = new Excel.Application();
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;

                // 创建工作簿和工作表
                workbook = excelApp.Workbooks.Add();
                worksheet = (Excel.Worksheet)workbook.Sheets[1];
                worksheet.Name = "交集汇总表";

                // 写入列标题
                for (int col = 0; col < ResultTable.Columns.Count; col++)
                {
                    worksheet.Cells[1, col + 1] = ResultTable.Columns[col].ColumnName;
                }

                // 设置标题行样式
                Excel.Range headerRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[1, ResultTable.Columns.Count]];
                headerRange.Font.Bold = true;
                headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(79, 129, 189));
                headerRange.Font.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.White);
                headerRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

                int dataRowCount = ResultTable.Rows.Count;
                int lastDataRow = dataRowCount; // 不含合计行的最后一行索引（0-based）

                // 写入数据行
                for (int row = 0; row < dataRowCount; row++)
                {
                    for (int col = 0; col < ResultTable.Columns.Count; col++)
                    {
                        var value = ResultTable.Rows[row][col];
                        if (value is double d)
                        {
                            worksheet.Cells[row + 2, col + 1] = Math.Round(d, DecimalPlaces);
                            // 设置数值格式
                            string format = DecimalPlaces > 0 ? $"0.{new string('0', DecimalPlaces)}" : "0";
                            ((Excel.Range)worksheet.Cells[row + 2, col + 1]).NumberFormat = format;
                        }
                        else
                        {
                            worksheet.Cells[row + 2, col + 1] = value?.ToString() ?? "";
                        }
                    }
                }

                // 合并相同值的单元格（按列处理，跳过最后一行合计和面积列）
                int mergeableColumns = ResultTable.Columns.Count - 1; // 面积列不合并
                int dataRows = lastDataRow - 1; // 不含合计行的数据行数

                // 辅助函数：获取指定行的区域键（用于判断是否属于同一分组）
                Func<int, string> getRegionKey = (rowIndex) =>
                {
                    if (_regionFieldCount == 0) return "";
                    var keys = new List<string>();
                    for (int c = 0; c < _regionFieldCount; c++)
                    {
                        keys.Add(ResultTable.Rows[rowIndex][c]?.ToString() ?? "");
                    }
                    return string.Join("|", keys);
                };

                for (int col = 0; col < mergeableColumns; col++)
                {
                    bool isRegionColumn = col < _regionFieldCount; // 是否为区域字段列
                    int mergeStartRow = 0; // 数据行索引（0-based）
                    string currentValue = ResultTable.Rows[0][col]?.ToString() ?? "";
                    string mergeStartRegionKey = getRegionKey(0);

                    for (int row = 1; row <= dataRows; row++)
                    {
                        bool shouldEndMerge = false;
                        string cellValue = row < dataRows ? (ResultTable.Rows[row][col]?.ToString() ?? "") : "";
                        string currentRegionKey = row < dataRows ? getRegionKey(row) : "";

                        if (row == dataRows)
                        {
                            // 到达合计行，结束合并
                            shouldEndMerge = true;
                        }
                        else if (cellValue == "合计")
                        {
                            // 遇到合计行
                            shouldEndMerge = true;
                        }
                        else if (cellValue != currentValue)
                        {
                            // 值不同
                            shouldEndMerge = true;
                        }
                        else if (!isRegionColumn && currentRegionKey != mergeStartRegionKey)
                        {
                            // 类字段列：区域分组变化，不能跨组合并
                            shouldEndMerge = true;
                        }

                        if (shouldEndMerge)
                        {
                            // 执行合并（如果有多行）
                            if (row > mergeStartRow + 1)
                            {
                                try
                                {
                                    int excelStartRow = mergeStartRow + 2; // Excel行从2开始
                                    int excelEndRow = row + 1; // 合并到当前行之前
                                    Excel.Range mergeRange = worksheet.Range[
                                        worksheet.Cells[excelStartRow, col + 1],
                                        worksheet.Cells[excelEndRow, col + 1]];
                                    mergeRange.Merge();
                                    mergeRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                                }
                                catch { }
                            }

                            // 重置起始位置
                            mergeStartRow = row;
                            currentValue = cellValue;
                            mergeStartRegionKey = currentRegionKey;
                        }
                    }
                }

                // 设置合计行样式（最后一行）
                if (dataRowCount > 0)
                {
                    Excel.Range totalRowRange = worksheet.Range[
                        worksheet.Cells[dataRowCount + 1, 1],
                        worksheet.Cells[dataRowCount + 1, ResultTable.Columns.Count]];
                    totalRowRange.Font.Bold = true;
                    totalRowRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(221, 235, 247));
                }

                // 自动调整列宽
                worksheet.Columns.AutoFit();

                // 添加边框
                Excel.Range dataRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[dataRowCount + 1, ResultTable.Columns.Count]];
                dataRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                dataRange.Borders.Weight = Excel.XlBorderWeight.xlThin;

                // 保存文件
                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook);
            }
            finally
            {
                // 清理资源
                if (workbook != null)
                {
                    workbook.Close(false);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                }
                if (excelApp != null)
                {
                    excelApp.Quit();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                }
                if (worksheet != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// 显示结果窗口
        /// </summary>
        private void ShowResultWindow()
        {
            if (ResultTable == null || ResultTable.Rows.Count == 0)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("没有可显示的数据", "提示");
                return;
            }

            try
            {
                var resultWindow = new IntersectSummaryResultWindow(ResultTable, DecimalPlaces, _regionFieldCount);
                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LogError($"显示结果窗口失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消操作
        /// </summary>
        private void Cancel()
        {
            CancelRequested = true;
            StatusMessage = "正在取消操作...";
            LogWarning("用户请求取消操作");
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private void ShowHelp()
        {
            var helpContent = "交集汇总表工具使用说明\n\n" +
                "功能描述：\n" +
                "计算区域红线与类要素图层的交集面积，并按区域和类别进行汇总统计。\n" +
                "支持面积调平，确保各子面积之和等于图斑实际面积。\n\n" +
                "参数说明：\n" +
                "• 区域图层：选择作为基础范围的面图层\n" +
                "• 区域字段(可选)：选择用于分组的字段，不选则不分组\n" +
                "• 类要素图层：选择需要计算交集的面图层\n" +
                "• 类字段：选择用于分类汇总的字段（支持多选）\n" +
                "• 输出单位：选择面积输出单位\n" +
                "• 保留位数：设置面积值的小数位数\n\n" +
                "面积调平说明：\n" +
                "工具会自动按比例调整交集面积，使各子面积之和\n" +
                "精确等于对应区域图斑的实际面积，消除计算误差。\n" +
                "保留小数位时使用最大余额法，确保四舍五入后总和不变。\n\n" +
                "操作步骤：\n" +
                "1. 选择区域图层\n" +
                "2. 可选：勾选区域字段进行分组\n" +
                "3. 选择类要素图层\n" +
                "4. 勾选需要的类字段\n" +
                "5. 设置输出单位和小数位数\n" +
                "6. 点击\"开始\"按钮执行计算\n" +
                "7. 计算完成后可查看或导出结果\n\n" +
                "单位默认小数位数：\n" +
                "• 平方米：2位小数\n" +
                "• 公顷：4位小数\n" +
                "• 亩：4位小数";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "交集汇总表工具帮助");
        }

        #region 辅助方法

        private void UpdateProgress(bool isIndeterminate, int progress)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsProgressIndeterminate = isIndeterminate;
                    Progress = progress;
                });
            }
        }

        private void UpdateStatus(string message)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = message;
                });
            }
        }

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";

            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LogContent += logMessage + Environment.NewLine;
                });
            }
        }

        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 警告: {message}";

            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LogContent += logMessage + Environment.NewLine;
                });
            }
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 错误: {message}";

            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LogContent += logMessage + Environment.NewLine;
                    StatusMessage = $"错误: {message}";
                });
            }
        }

        #endregion
    }
}
