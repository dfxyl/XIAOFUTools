using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Tools.AreaCalculator
{
    /// <summary>
    /// 字段显示信息类
    /// </summary>
    public class FieldDisplayInfo
    {
        public string FieldName { get; set; }
        public string Alias { get; set; }
        public string FieldType { get; set; }

        public string DisplayText => string.IsNullOrEmpty(Alias) || Alias == FieldName
            ? $"{FieldName}（{FieldType}）"
            : $"{FieldName}（{Alias}）（{FieldType}）";

        public override string ToString() => DisplayText;
    }

    /// <summary>
    /// 计算面积DockPane视图模型
    /// </summary>
    internal class AreaCalculatorDockPaneViewModel : PropertyChangedBase
    {
        #region 属性

        // 取消操作标志
        private bool _cancelRequested = false;
        public bool CancelRequested
        {
            get => _cancelRequested;
            set
            {
                SetProperty(ref _cancelRequested, value);
            }
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
        
        // 是否可以处理
        public bool CanProcess => !IsProcessing && HasSelectedLayer && !string.IsNullOrEmpty(SelectedFieldName);

        // 面图层列表
        private ObservableCollection<FeatureLayer> _polygonLayers;
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set
            {
                SetProperty(ref _polygonLayers, value);
            }
        }

        // 选中的面图层
        private FeatureLayer _selectedPolygonLayer;
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                NotifyPropertyChanged(() => HasSelectedLayer);
                NotifyPropertyChanged(() => CanProcess);
                LoadFieldNames();
            }
        }

        // 是否有选中图层
        public bool HasSelectedLayer => SelectedPolygonLayer != null;

        // 优先选中的图层名称（用于右键菜单打开时自动定位）
        private string _preferredLayerName;
        public string PreferredLayerName
        {
            get => _preferredLayerName;
            set => SetProperty(ref _preferredLayerName, value);
        }

        // 字段信息列表
        private ObservableCollection<FieldDisplayInfo> _fieldInfos;
        public ObservableCollection<FieldDisplayInfo> FieldInfos
        {
            get => _fieldInfos;
            set
            {
                SetProperty(ref _fieldInfos, value);
            }
        }

        // 选中的字段信息
        private FieldDisplayInfo _selectedFieldInfo;
        public FieldDisplayInfo SelectedFieldInfo
        {
            get => _selectedFieldInfo;
            set
            {
                SetProperty(ref _selectedFieldInfo, value);
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        // 选中的字段名称（用于内部处理）
        public string SelectedFieldName => SelectedFieldInfo?.FieldName;

        // 面积单位列表
        private ObservableCollection<string> _areaUnits;
        public ObservableCollection<string> AreaUnits
        {
            get => _areaUnits;
            set
            {
                SetProperty(ref _areaUnits, value);
            }
        }

        // 选中的面积单位
        private string _selectedAreaUnit;
        public string SelectedAreaUnit
        {
            get => _selectedAreaUnit;
            set
            {
                SetProperty(ref _selectedAreaUnit, value);
            }
        }

        // 保留小数位数
        private int _decimalPlaces = 2;
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set
            {
                SetProperty(ref _decimalPlaces, value);
            }
        }

        // 面积类型列表
        private ObservableCollection<string> _areaTypes;
        public ObservableCollection<string> AreaTypes
        {
            get => _areaTypes;
            set
            {
                SetProperty(ref _areaTypes, value);
            }
        }

        // 选中的面积类型
        private string _selectedAreaType;
        public string SelectedAreaType
        {
            get => _selectedAreaType;
            set
            {
                SetProperty(ref _selectedAreaType, value);
                NotifyPropertyChanged(() => IsEllipsoidSelected);
            }
        }

        // 是否选择了椭球面积
        public bool IsEllipsoidSelected => SelectedAreaType == "椭球";

        // 进度值
        private int _progress = 0;
        public int Progress
        {
            get => _progress;
            set
            {
                SetProperty(ref _progress, value);
            }
        }

        // 进度条是否不确定
        private bool _isProgressIndeterminate = false;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set
            {
                SetProperty(ref _isProgressIndeterminate, value);
            }
        }

        // 状态消息
        private string _statusMessage = "请选择面图层和字段。";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                SetProperty(ref _statusMessage, value);
            }
        }

        // 日志内容
        private string _logContent = "";
        public string LogContent
        {
            get => _logContent;
            set
            {
                SetProperty(ref _logContent, value);
            }
        }

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

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public AreaCalculatorDockPaneViewModel()
        {
            // 初始化属性
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            FieldInfos = new ObservableCollection<FieldDisplayInfo>();

            // 初始化面积单位
            AreaUnits = new ObservableCollection<string> { "平方米", "公顷", "亩", "平方公里" };
            SelectedAreaUnit = "平方米";

            // 初始化面积类型
            AreaTypes = new ObservableCollection<string> { "平面", "椭球" };
            SelectedAreaType = "平面";

            StatusMessage = "请选择面图层和字段。";
            LogContent = "";
            Progress = 0;
            IsProgressIndeterminate = false;

            // 加载面图层
            LoadPolygonLayers();
        }

        /// <summary>
        /// 刷新图层列表（供DockPane调用）
        /// </summary>
        public void RefreshLayers()
        {
            LoadPolygonLayers();
        }

        /// <summary>
        /// 设置优先选中的图层名称
        /// </summary>
        public void SetPreferredLayerName(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return;
            }

            PreferredLayerName = layerName;

            // 图层已加载时立即尝试匹配
            if (PolygonLayers != null && PolygonLayers.Count > 0)
            {
                var matchedLayer = PolygonLayers.FirstOrDefault(layer =>
                    string.Equals(layer.Name, PreferredLayerName, StringComparison.OrdinalIgnoreCase));

                if (matchedLayer != null)
                {
                    SelectedPolygonLayer = matchedLayer;
                }
            }
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
                            // 清空图层列表
                            PolygonLayers?.Clear();

                            // 添加图层
                            if (PolygonLayers != null)
                            {
                                foreach (var layer in tempLayers)
                                {
                                    PolygonLayers.Add(layer);
                                }

                                // 如果有图层，默认选择第一个
                                if (PolygonLayers.Count > 0)
                                {
                                    var preferredLayer = !string.IsNullOrWhiteSpace(PreferredLayerName)
                                        ? PolygonLayers.FirstOrDefault(layer =>
                                            string.Equals(layer.Name, PreferredLayerName, StringComparison.OrdinalIgnoreCase))
                                        : null;

                                    SelectedPolygonLayer = preferredLayer ?? PolygonLayers[0];
                                }
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
                            StatusMessage = $"加载图层出错: {ex.Message}";
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 加载字段信息
        /// </summary>
        private void LoadFieldNames()
        {
            if (SelectedPolygonLayer == null)
            {
                FieldInfos?.Clear();
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var tempFieldInfos = new List<FieldDisplayInfo>();

                    await QueuedTask.Run(() =>
                    {
                        var featureClass = SelectedPolygonLayer.GetFeatureClass();
                        if (featureClass != null)
                        {
                            var definition = featureClass.GetDefinition();
                            var fields = definition.GetFields();

                            foreach (var field in fields)
                            {
                                // 显示数值类型字段和文本字段
                                if (field.FieldType == FieldType.Double ||
                                    field.FieldType == FieldType.Single ||
                                    field.FieldType == FieldType.Integer ||
                                    field.FieldType == FieldType.SmallInteger ||
                                    field.FieldType == FieldType.String)
                                {
                                    var fieldInfo = new FieldDisplayInfo
                                    {
                                        FieldName = field.Name,
                                        Alias = field.AliasName,
                                        FieldType = GetFieldTypeDisplayName(field.FieldType)
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
                            FieldInfos?.Clear();
                            if (FieldInfos != null)
                            {
                                foreach (var fieldInfo in tempFieldInfos)
                                {
                                    FieldInfos.Add(fieldInfo);
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
                FieldType.DateOnly => "仅日期",
                FieldType.TimeOnly => "仅时间",
                FieldType.TimestampOffset => "时间戳偏移",
                FieldType.GUID => "全局唯一标识符",
                FieldType.GlobalID => "全局ID",
                FieldType.OID => "对象ID",
                FieldType.Geometry => "几何",
                FieldType.Blob => "二进制大对象",
                FieldType.Raster => "栅格",
                FieldType.XML => "XML",
                _ => "未知类型"
            };
        }

        /// <summary>
        /// 执行面积计算
        /// </summary>
        private async Task ExecuteAsync()
        {
            if (SelectedPolygonLayer == null || string.IsNullOrEmpty(SelectedFieldName))
            {
                StatusMessage = "请选择面图层和字段。";
                return;
            }

            try
            {
                IsProcessing = true;
                CancelRequested = false;
                Progress = 0;
                IsProgressIndeterminate = true;
                StatusMessage = "正在计算面积...";
                LogContent = "";

                LogInfo($"开始计算面积 - 图层: {SelectedPolygonLayer.Name}, 字段: {SelectedFieldName}");
                LogInfo($"单位: {SelectedAreaUnit}, 小数位数: {DecimalPlaces}, 类型: {SelectedAreaType}");

                await QueuedTask.Run(() =>
                {
                    try
                    {
                        // 检查取消请求
                        if (CancelRequested)
                        {
                            LogWarning("操作已取消");
                            return;
                        }

                        // 获取要素类
                        var featureClass = SelectedPolygonLayer.GetFeatureClass();
                        if (featureClass == null)
                        {
                            LogError("无法获取要素类");
                            return;
                        }

                        // 获取要素总数
                        var totalCount = (long)featureClass.GetCount();
                        LogInfo($"共有 {totalCount} 个要素需要处理");

                        if (totalCount == 0)
                        {
                            if (System.Windows.Application.Current?.Dispatcher != null)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    StatusMessage = "图层中没有可处理的要素。";
                                    Progress = 100;
                                });
                            }

                            return;
                        }

                        // 更新进度条为确定模式
                        if (System.Windows.Application.Current?.Dispatcher != null)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                IsProgressIndeterminate = false;
                                Progress = 0;
                            });
                        }

                        // 获取字段类型信息
                        FieldType targetFieldType = FieldType.Double;
                        try
                        {
                            var fieldDef = featureClass.GetDefinition().GetFields().FirstOrDefault(f => f.Name == SelectedFieldName);
                            if (fieldDef != null)
                            {
                                targetFieldType = fieldDef.FieldType;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWarning($"获取字段类型失败: {ex.Message}");
                        }

                        // 处理要素并直接写入数据（无需手动开启编辑会话）
                        var updatedCount = ProcessFeatures(featureClass, totalCount, SelectedFieldName, SelectedAreaUnit, DecimalPlaces, SelectedAreaType, targetFieldType);

                        if (!CancelRequested)
                        {
                            LogInfo($"面积计算完成，共写入 {updatedCount} 个要素！");

                            if (System.Windows.Application.Current?.Dispatcher != null)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    StatusMessage = $"处理完成，已更新 {updatedCount} 个要素。";
                                    Progress = 100;
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理过程中出错: {ex.Message}");
                    }
                });
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
        /// 处理要素
        /// </summary>
        private int ProcessFeatures(FeatureClass featureClass, long totalCount,
            string fieldName, string areaUnit, int decimalPlaces, string areaType, FieldType targetFieldType)
        {
            var processedCount = 0L;
            var updatedCount = 0;
            var batchSize = 100; // 批处理大小

            using (var cursor = featureClass.Search(new QueryFilter(), false))
            {
                while (cursor.MoveNext())
                {
                    if (CancelRequested)
                    {
                        LogWarning("操作已取消");
                        break;
                    }

                    using (var feature = cursor.Current as Feature)
                    {
                        if (feature != null)
                        {
                            try
                            {
                                // 计算面积
                                var area = CalculateAreaByType(feature.GetShape() as Polygon, areaType);

                                // 转换单位
                                var convertedArea = ConvertAreaUnit(area, areaUnit);

                                // 格式化面积值
                                var formattedValue = FormatAreaValueByFieldType(convertedArea, decimalPlaces, targetFieldType);

                                // 直接写入目标字段（无需编辑操作）
                                feature[fieldName] = formattedValue;
                                feature.Store();
                                updatedCount++;
                            }
                            catch (Exception ex)
                            {
                                LogWarning($"处理要素 {feature.GetObjectID()} 时出错: {ex.Message}");
                            }

                            processedCount++;

                            // 更新进度
                            if (processedCount % batchSize == 0 || processedCount == totalCount)
                            {
                                var progressPercent = (int)((double)processedCount / totalCount * 100);

                                if (System.Windows.Application.Current?.Dispatcher != null)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        Progress = progressPercent;
                                        StatusMessage = $"正在处理... ({processedCount}/{totalCount})";
                                    });
                                }

                                LogInfo($"已处理 {processedCount}/{totalCount} 个要素");
                            }
                        }
                    }
                }
            }

            return updatedCount;
        }

        /// <summary>
        /// 计算面积
        /// </summary>
        private double CalculateArea(Polygon polygon)
        {
            if (polygon == null) return 0;

            if (SelectedAreaType == "椭球")
            {
                // 椭球面积计算
                return GeometryEngine.Instance.GeodesicArea(polygon);
            }
            else
            {
                // 平面面积计算
                return polygon.Area;
            }
        }

        /// <summary>
        /// 根据类型计算面积（线程安全版本）
        /// </summary>
        private double CalculateAreaByType(Polygon polygon, string areaType)
        {
            if (polygon == null) return 0;

            if (areaType == "椭球")
            {
                // 椭球面积计算 - 使用ArcGIS内置椭球面积计算
                return GeometryEngine.Instance.GeodesicArea(polygon);
            }
            else
            {
                // 平面面积计算
                return polygon.Area;
            }
        }

        /// <summary>
        /// 转换面积单位
        /// </summary>
        private double ConvertAreaUnit(double areaInSquareMeters, string targetUnit)
        {
            switch (targetUnit)
            {
                case "平方米":
                    return areaInSquareMeters;
                case "公顷":
                    return areaInSquareMeters / 10000.0;
                case "亩":
                    return areaInSquareMeters / 666.67;
                case "平方公里":
                    return areaInSquareMeters / 1000000.0;
                default:
                    return areaInSquareMeters;
            }
        }

        /// <summary>
        /// 格式化面积值，根据目标字段类型返回适当的值
        /// </summary>
        private object FormatAreaValue(double area, int decimalPlaces)
        {
            if (SelectedFieldInfo == null)
                return area;

            // 根据字段类型确定如何格式化
            var fieldType = GetFieldTypeFromDisplayName(SelectedFieldInfo.FieldType);

            switch (fieldType)
            {
                case FieldType.String:
                    // 文本字段：格式化为带指定小数位数的字符串，保留尾随零
                    var formatString = decimalPlaces > 0 ? $"F{decimalPlaces}" : "F0";
                    return area.ToString(formatString);

                case FieldType.Integer:
                case FieldType.SmallInteger:
                    // 整型字段：四舍五入为整数
                    return (int)Math.Round(area);

                case FieldType.Double:
                case FieldType.Single:
                default:
                    // 数值字段：保留指定小数位数
                    return Math.Round(area, decimalPlaces);
            }
        }

        /// <summary>
        /// 从显示名称获取字段类型
        /// </summary>
        private FieldType GetFieldTypeFromDisplayName(string displayName)
        {
            return displayName switch
            {
                "双精度" => FieldType.Double,
                "单精度" => FieldType.Single,
                "整型" => FieldType.Integer,
                "短整型" => FieldType.SmallInteger,
                "文本" => FieldType.String,
                _ => FieldType.Double
            };
        }

        /// <summary>
        /// 根据字段类型格式化面积值（线程安全版本）
        /// </summary>
        private object FormatAreaValueByFieldType(double area, int decimalPlaces, FieldType fieldType)
        {
            switch (fieldType)
            {
                case FieldType.String:
                    // 文本字段：格式化为带指定小数位数的字符串，保留尾随零
                    var formatString = decimalPlaces > 0 ? $"F{decimalPlaces}" : "F0";
                    return area.ToString(formatString);

                case FieldType.Integer:
                case FieldType.SmallInteger:
                    // 整型字段：四舍五入为整数
                    return (int)Math.Round(area);

                case FieldType.Double:
                case FieldType.Single:
                default:
                    // 数值字段：保留指定小数位数
                    return Math.Round(area, decimalPlaces);
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
            var helpContent = "计算面积工具使用说明\n\n" +
                "功能描述：\n" +
                "计算面要素的面积并写入指定字段。\n\n" +
                "参数说明：\n" +
                "• 面图层：选择要计算面积的面要素图层\n" +
                "• 字段：选择用于存储面积值的字段（支持数值字段和文本字段）\n" +
                "• 单位：选择面积计算单位（平方米、公顷、亩、平方公里）\n" +
                "• 保留小数位数：设置面积值的小数位数\n" +
                "• 面积类型：选择计算方式（平面或椭球）\n\n" +
                "操作步骤：\n" +
                "1. 选择要处理的面图层\n" +
                "2. 选择用于存储面积的字段（支持数值类型和文本类型）\n" +
                "3. 设置面积单位和小数位数\n" +
                "4. 选择面积计算类型\n" +
                "5. 点击\"开始\"按钮执行计算\n" +
                "6. 处理过程中可点击\"停止\"按钮取消操作\n\n" +
                "面积类型说明：\n" +
                "• 平面：基于投影坐标系统的平面面积计算，适用于小范围区域\n" +
                "• 椭球：基于地理坐标系统的椭球面积计算，适用于大范围区域的精确计算\n" +
                "  椭球面积计算符合测绘规范要求，提供高精度的面积计算结果\n\n" +
                "单位换算：\n" +
                "• 1公顷 = 10,000平方米\n" +
                "• 1亩 ≈ 666.67平方米\n" +
                "• 1平方公里 = 1,000,000平方米\n\n" +
                "字段类型说明：\n" +
                "• 数值字段：直接存储计算结果，根据小数位数四舍五入\n" +
                "• 整型字段：自动四舍五入为整数\n" +
                "• 文本字段：存储格式化的字符串，保留指定小数位数（包括尾随零）\n\n" +
                "注意事项：\n" +
                "• 支持数值字段和文本字段存储面积值\n" +
                "• 椭球面积计算需要图层具有地理坐标系统\n" +
                "• 大数据量处理时建议先备份数据\n" +
                "• 处理过程中会修改原始数据，请谨慎操作";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "计算面积工具帮助");
        }

        #region 日志方法

        /// <summary>
        /// 记录信息日志
        /// </summary>
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

        /// <summary>
        /// 记录警告日志
        /// </summary>
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

        /// <summary>
        /// 记录错误日志
        /// </summary>
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
