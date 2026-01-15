using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Tools.MultiOverlaySummary
{
    public class FieldSelectItem : PropertyChangedBase
    {
        public string FieldName { get; set; }
        public string Alias { get; set; }
        public string FieldType { get; set; }

        public string DisplayText => string.IsNullOrEmpty(Alias) || Alias == FieldName
            ? $"{FieldName}（{FieldType}）"
            : $"{FieldName}（{Alias}）（{FieldType}）";

        public override string ToString() => DisplayText;
    }

    public class OverlayLayerItem : PropertyChangedBase
    {
        public FeatureLayer Layer { get; set; }
        public string LayerName => Layer?.Name ?? "";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    /// <summary>
    /// 交集几何结果项
    /// </summary>
    public class IntersectGeometryItem
    {
        public string GroupKey { get; set; }
        public string OverlayLayerName { get; set; }
        public Polygon Geometry { get; set; }
        public double Area { get; set; }
    }

    internal class MultiOverlaySummaryDockPaneViewModel : PropertyChangedBase
    {
        #region 属性

        private bool _cancelRequested = false;
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
        }

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

        public bool CanProcess => !IsProcessing &&
                                  SelectedMainLayer != null &&
                                  OverlayLayerItems?.Any(l => l.IsSelected) == true;

        private ObservableCollection<FeatureLayer> _polygonLayers;
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }

        private FeatureLayer _selectedMainLayer;
        public FeatureLayer SelectedMainLayer
        {
            get => _selectedMainLayer;
            set
            {
                SetProperty(ref _selectedMainLayer, value);
                NotifyPropertyChanged(() => CanProcess);
                LoadMainLayerFields();
                UpdateOverlayLayerItems();
            }
        }

        private ObservableCollection<FieldSelectItem> _mainLayerFields;
        public ObservableCollection<FieldSelectItem> MainLayerFields
        {
            get => _mainLayerFields;
            set => SetProperty(ref _mainLayerFields, value);
        }

        private FieldSelectItem _selectedUniqueField;
        public FieldSelectItem SelectedUniqueField
        {
            get => _selectedUniqueField;
            set => SetProperty(ref _selectedUniqueField, value);
        }

        private ObservableCollection<OverlayLayerItem> _overlayLayerItems;
        public ObservableCollection<OverlayLayerItem> OverlayLayerItems
        {
            get => _overlayLayerItems;
            set => SetProperty(ref _overlayLayerItems, value);
        }

        private ObservableCollection<string> _areaUnits;
        public ObservableCollection<string> AreaUnits
        {
            get => _areaUnits;
            set => SetProperty(ref _areaUnits, value);
        }

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

        private int _decimalPlaces = 2;
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set => SetProperty(ref _decimalPlaces, value);
        }

        private int _progress = 0;
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private bool _isProgressIndeterminate = false;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        private string _statusMessage = "请选择图层。";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _logContent = "";
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        private DataTable _resultTable;
        public DataTable ResultTable
        {
            get => _resultTable;
            set => SetProperty(ref _resultTable, value);
        }

        public bool HasResult => ResultTable != null && ResultTable.Rows.Count > 0;

        // 保存交集几何用于导出SHP
        private List<IntersectGeometryItem> _intersectGeometries;
        private SpatialReference _spatialReference;
        private string _mainLayerName;

        #endregion

        #region 命令

        private ICommand _runCommand;
        public ICommand RunCommand => _runCommand ?? (_runCommand = new RelayCommand(async () => await ExecuteAsync(), () => CanProcess));

        private ICommand _cancelCommand;
        public ICommand CancelCommand => _cancelCommand ?? (_cancelCommand = new RelayCommand(() => Cancel(), () => IsProcessing));

        private ICommand _showHelpCommand;
        public ICommand ShowHelpCommand => _showHelpCommand ?? (_showHelpCommand = new RelayCommand(() => ShowHelp()));

        private ICommand _refreshLayersCommand;
        public ICommand RefreshLayersCommand => _refreshLayersCommand ?? (_refreshLayersCommand = new RelayCommand(() => RefreshLayers()));

        private ICommand _exportCommand;
        public ICommand ExportCommand => _exportCommand ?? (_exportCommand = new RelayCommand(async () => await ShowExportOptionsAsync(), () => HasResult));

        private ICommand _showResultCommand;
        public ICommand ShowResultCommand => _showResultCommand ?? (_showResultCommand = new RelayCommand(() => ShowResultWindow(), () => HasResult));

        #endregion

        public MultiOverlaySummaryDockPaneViewModel()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            MainLayerFields = new ObservableCollection<FieldSelectItem>();
            OverlayLayerItems = new ObservableCollection<OverlayLayerItem>();
            AreaUnits = new ObservableCollection<string> { "平方米", "公顷", "亩" };
            SelectedAreaUnit = "平方米";
            _intersectGeometries = new List<IntersectGeometryItem>();
            LoadPolygonLayers();
        }

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

        public void RefreshLayers() => LoadPolygonLayers();

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

                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        PolygonLayers?.Clear();
                        foreach (var layer in tempLayers)
                            PolygonLayers?.Add(layer);
                        UpdateOverlayLayerItems();
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        StatusMessage = $"加载图层出错: {ex.Message}";
                    });
                }
            });
        }

        private void LoadMainLayerFields()
        {
            if (SelectedMainLayer == null)
            {
                MainLayerFields?.Clear();
                SelectedUniqueField = null;
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var tempFieldInfos = new List<FieldSelectItem>();
                    await QueuedTask.Run(() =>
                    {
                        var featureClass = SelectedMainLayer.GetFeatureClass();
                        if (featureClass != null)
                        {
                            var definition = featureClass.GetDefinition();
                            foreach (var field in definition.GetFields())
                            {
                                if (field.FieldType == FieldType.Geometry ||
                                    field.FieldType == FieldType.OID ||
                                    field.FieldType == FieldType.GlobalID ||
                                    field.FieldType == FieldType.Blob ||
                                    field.FieldType == FieldType.Raster)
                                    continue;

                                tempFieldInfos.Add(new FieldSelectItem
                                {
                                    FieldName = field.Name,
                                    Alias = field.AliasName,
                                    FieldType = GetFieldTypeDisplayName(field.FieldType)
                                });
                            }
                        }
                    });

                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        MainLayerFields?.Clear();
                        MainLayerFields?.Add(new FieldSelectItem { FieldName = "", Alias = "（不分组）", FieldType = "" });
                        foreach (var fieldInfo in tempFieldInfos)
                            MainLayerFields?.Add(fieldInfo);
                        SelectedUniqueField = MainLayerFields?.FirstOrDefault();
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        StatusMessage = $"加载字段出错: {ex.Message}";
                    });
                }
            });
        }

        private void UpdateOverlayLayerItems()
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                var previousSelections = OverlayLayerItems?.Where(x => x.IsSelected).Select(x => x.LayerName).ToList() ?? new List<string>();
                OverlayLayerItems?.Clear();

                foreach (var layer in PolygonLayers)
                {
                    if (layer != SelectedMainLayer)
                    {
                        var item = new OverlayLayerItem { Layer = layer, IsSelected = previousSelections.Contains(layer.Name) };
                        item.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(OverlayLayerItem.IsSelected))
                                NotifyPropertyChanged(() => CanProcess);
                        };
                        OverlayLayerItems?.Add(item);
                    }
                }
            });
        }

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

        private async Task ExecuteAsync()
        {
            if (SelectedMainLayer == null)
            {
                StatusMessage = "请选择主图层。";
                return;
            }

            var selectedOverlayLayers = OverlayLayerItems?.Where(l => l.IsSelected).Select(l => l.Layer).ToList();
            if (selectedOverlayLayers == null || !selectedOverlayLayers.Any())
            {
                StatusMessage = "请至少选择一个压盖图层。";
                return;
            }

            try
            {
                IsProcessing = true;
                CancelRequested = false;
                Progress = 0;
                IsProgressIndeterminate = true;
                StatusMessage = "正在计算多图层压盖汇总...";
                LogContent = "";

                string uniqueFieldName = string.IsNullOrEmpty(SelectedUniqueField?.FieldName) ? null : SelectedUniqueField.FieldName;
                bool useGrouping = !string.IsNullOrEmpty(uniqueFieldName);
                _mainLayerName = SelectedMainLayer.Name;

                LogInfo($"开始计算多图层压盖汇总");
                LogInfo($"主图层: {_mainLayerName}");
                LogInfo($"唯一字段: {(useGrouping ? uniqueFieldName : "（不分组-汇总全部）")}");
                LogInfo($"压盖图层: {string.Join(", ", selectedOverlayLayers.Select(l => l.Name))}");

                var results = new Dictionary<string, Dictionary<string, double>>();
                var mainFeatureAreas = new Dictionary<string, double>();
                _intersectGeometries = new List<IntersectGeometryItem>();

                await QueuedTask.Run(() =>
                {
                    if (CancelRequested) return;

                    var mainFC = SelectedMainLayer.GetFeatureClass();
                    if (mainFC == null)
                    {
                        LogError("无法获取主图层要素类");
                        return;
                    }

                    _spatialReference = mainFC.GetDefinition().GetSpatialReference();

                    var mainFeatures = new List<(string Key, Polygon Geometry)>();
                    using (var cursor = mainFC.Search())
                    {
                        while (cursor.MoveNext())
                        {
                            if (CancelRequested) return;
                            using (var feature = cursor.Current as Feature)
                            {
                                if (feature?.GetShape() is Polygon polygon)
                                {
                                    string key = useGrouping
                                        ? (feature[uniqueFieldName]?.ToString() ?? "(空值)")
                                        : "全部";
                                    mainFeatures.Add((key, polygon));
                                }
                            }
                        }
                    }

                    LogInfo($"共有 {mainFeatures.Count} 个主图层要素");
                    UpdateProgress(false, 0);

                    int totalSteps = mainFeatures.Count * selectedOverlayLayers.Count;
                    int currentStep = 0;

                    var uniqueKeys = mainFeatures.Select(f => f.Key).Distinct().ToList();
                    foreach (var key in uniqueKeys)
                    {
                        results[key] = new Dictionary<string, double>();
                        mainFeatureAreas[key] = 0;
                        foreach (var overlayLayer in selectedOverlayLayers)
                            results[key][overlayLayer.Name] = 0;
                    }

                    foreach (var mainFeature in mainFeatures)
                    {
                        if (CancelRequested) { LogWarning("操作已取消"); return; }

                        double mainArea = CalculateArea(mainFeature.Geometry);
                        mainFeatureAreas[mainFeature.Key] += mainArea;

                        foreach (var overlayLayer in selectedOverlayLayers)
                        {
                            if (CancelRequested) return;

                            var overlayFC = overlayLayer.GetFeatureClass();
                            if (overlayFC == null) continue;

                            // 获取压盖图层的空间参考
                            var overlaySR = overlayFC.GetDefinition().GetSpatialReference();

                            // 将主图层几何投影到压盖图层坐标系进行空间查询
                            Geometry queryGeometry = mainFeature.Geometry;
                            if (_spatialReference != null && overlaySR != null && !SpatialReference.AreEqual(_spatialReference, overlaySR, false))
                            {
                                queryGeometry = GeometryEngine.Instance.Project(mainFeature.Geometry, overlaySR);
                            }

                            var spatialFilter = new SpatialQueryFilter
                            {
                                FilterGeometry = queryGeometry,
                                SpatialRelationship = SpatialRelationship.Intersects
                            };

                            using (var overlayCursor = overlayFC.Search(spatialFilter))
                            {
                                while (overlayCursor.MoveNext())
                                {
                                    if (CancelRequested) return;
                                    using (var overlayFeature = overlayCursor.Current as Feature)
                                    {
                                        if (overlayFeature?.GetShape() is Polygon overlayPolygon)
                                        {
                                            // 将压盖图层几何投影到主图层坐标系进行交集运算
                                            Polygon projectedOverlay = overlayPolygon;
                                            if (_spatialReference != null && overlaySR != null && !SpatialReference.AreEqual(_spatialReference, overlaySR, false))
                                            {
                                                projectedOverlay = GeometryEngine.Instance.Project(overlayPolygon, _spatialReference) as Polygon;
                                            }

                                            if (projectedOverlay == null) continue;

                                            var intersection = GeometryEngine.Instance.Intersection(mainFeature.Geometry, projectedOverlay);
                                            if (intersection != null && !intersection.IsEmpty && intersection is Polygon intersectPolygon)
                                            {
                                                double area = CalculateArea(intersectPolygon);
                                                results[mainFeature.Key][overlayLayer.Name] += area;

                                                // 保存交集几何
                                                _intersectGeometries.Add(new IntersectGeometryItem
                                                {
                                                    GroupKey = mainFeature.Key,
                                                    OverlayLayerName = overlayLayer.Name,
                                                    Geometry = intersectPolygon,
                                                    Area = area
                                                });
                                            }
                                        }
                                    }
                                }
                            }

                            currentStep++;
                            UpdateProgress(false, (int)((double)currentStep / totalSteps * 100));
                            UpdateStatus($"正在处理... ({currentStep}/{totalSteps})");
                        }
                    }

                    LogInfo($"压盖计算完成，共 {_intersectGeometries.Count} 个交集图形");
                });

                if (!CancelRequested && results.Count > 0)
                {
                    CreateResultTable(results, mainFeatureAreas, selectedOverlayLayers.Select(l => l.Name).ToList(), uniqueFieldName);
                    LogInfo($"汇总完成，共 {results.Count} 条记录");
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
                    StatusMessage = "未找到数据";
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

        private double CalculateArea(Polygon polygon)
        {
            if (polygon == null || polygon.IsEmpty) return 0;
            if (polygon.SpatialReference != null && polygon.SpatialReference.IsGeographic)
                return Math.Abs(GeometryEngine.Instance.GeodesicArea(polygon));
            return Math.Abs(polygon.Area);
        }

        private void CreateResultTable(Dictionary<string, Dictionary<string, double>> results,
            Dictionary<string, double> mainFeatureAreas, List<string> overlayLayerNames, string uniqueFieldName)
        {
            var table = new DataTable();

            // 唯一字段列名
            string keyColumnName = string.IsNullOrEmpty(uniqueFieldName) ? "分组"
                : (SelectedUniqueField?.Alias ?? uniqueFieldName);
            table.Columns.Add(keyColumnName, typeof(string));

            // 主图层面积列 - 使用主图层名称
            string mainAreaColumnName = $"{_mainLayerName}({SelectedAreaUnit})";
            table.Columns.Add(mainAreaColumnName, typeof(double));

            // 压盖图层面积列
            foreach (var layerName in overlayLayerNames)
                table.Columns.Add($"{layerName}({SelectedAreaUnit})", typeof(double));

            foreach (var kvp in results.OrderBy(x => x.Key))
            {
                var row = table.NewRow();
                row[0] = kvp.Key;
                row[1] = ConvertAreaUnit(mainFeatureAreas[kvp.Key], SelectedAreaUnit);

                int colIndex = 2;
                foreach (var layerName in overlayLayerNames)
                {
                    double area = kvp.Value.ContainsKey(layerName) ? kvp.Value[layerName] : 0;
                    row[colIndex++] = ConvertAreaUnit(area, SelectedAreaUnit);
                }
                table.Rows.Add(row);
            }

            // 合计行
            var totalRow = table.NewRow();
            totalRow[0] = "合计";
            totalRow[1] = Math.Round(table.AsEnumerable().Sum(r => Convert.ToDouble(r[1])), DecimalPlaces);
            for (int i = 2; i < table.Columns.Count; i++)
                totalRow[i] = Math.Round(table.AsEnumerable().Sum(r => Convert.ToDouble(r[i])), DecimalPlaces);
            table.Rows.Add(totalRow);

            // 应用小数位数
            foreach (DataRow row in table.Rows)
            {
                for (int i = 1; i < table.Columns.Count; i++)
                {
                    if (row[i] is double d)
                        row[i] = Math.Round(d, DecimalPlaces);
                }
            }

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                ResultTable = table;
                NotifyPropertyChanged(() => HasResult);
            });
        }

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

        private async Task ShowExportOptionsAsync()
        {
            if (ResultTable == null || ResultTable.Rows.Count == 0)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("没有可导出的数据", "提示");
                return;
            }

            var dialog = new ExportOptionsDialog();
            if (dialog.ShowDialog() != true)
                return;

            // 选择输出文件夹
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择导出文件夹",
                ShowNewFolderButton = true
            };

            if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            string outputFolder = folderDialog.SelectedPath;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var results = new List<string>();

            try
            {
                IsProcessing = true;

                // 导出Excel
                if (dialog.ExportExcel)
                {
                    string excelPath = Path.Combine(outputFolder, $"多图层压盖汇总表_{timestamp}.xlsx");
                    ExportToExcel(excelPath);
                    results.Add($"表格: {excelPath}");
                    LogInfo($"已导出表格: {excelPath}");
                }

                // 导出GDB
                if (dialog.ExportGdb && _intersectGeometries?.Count > 0)
                {
                    string gdbPath = await ExportToGdbAsync(outputFolder, timestamp);
                    if (!string.IsNullOrEmpty(gdbPath))
                        results.Add($"GDB: {gdbPath}");
                }

                StatusMessage = "导出完成！";
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"导出完成！\n\n{string.Join("\n", results)}", "成功");
            }
            catch (Exception ex)
            {
                LogError($"导出失败: {ex.Message}");
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"导出失败: {ex.Message}", "错误");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task<string> ExportToGdbAsync(string outputFolder, string timestamp)
        {
            if (_intersectGeometries == null || _intersectGeometries.Count == 0)
                return null;

            string gdbName = $"压盖分析_{timestamp}.gdb";
            string gdbPath = Path.Combine(outputFolder, gdbName);

            StatusMessage = "正在导出GDB...";
            LogInfo($"开始导出GDB到: {gdbPath}");

            var groupedByLayer = _intersectGeometries.GroupBy(g => g.OverlayLayerName);
            int exportedCount = 0;
            string areaFieldName = GetAreaFieldName();

            await QueuedTask.Run(() =>
            {
                var createGdbParams = Geoprocessing.MakeValueArray(outputFolder, gdbName);
                var createGdbResult = Geoprocessing.ExecuteToolAsync("management.CreateFileGDB", createGdbParams).Result;

                if (createGdbResult.IsFailed)
                {
                    LogError("创建GDB失败");
                    return;
                }

                using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath))))
                {
                    foreach (var layerGroup in groupedByLayer)
                    {
                        string layerName = layerGroup.Key;
                        string safeLayerName = MakeSafeFileName(layerName);
                        string fcName = $"压盖_{safeLayerName}";

                        try
                        {
                            var fieldDescriptions = new List<ArcGIS.Core.Data.DDL.FieldDescription>
                            {
                                new ArcGIS.Core.Data.DDL.FieldDescription("GroupKey", FieldType.String) { Length = 100 },
                                new ArcGIS.Core.Data.DDL.FieldDescription("LayerName", FieldType.String) { Length = 100 },
                                new ArcGIS.Core.Data.DDL.FieldDescription(areaFieldName, FieldType.Double)
                            };

                            var shapeDescription = new ShapeDescription(GeometryType.Polygon, _spatialReference);
                            var fcDescription = new FeatureClassDescription(fcName, fieldDescriptions, shapeDescription);

                            var schemaBuilder = new SchemaBuilder(geodatabase);
                            schemaBuilder.Create(fcDescription);

                            if (!schemaBuilder.Build())
                            {
                                LogError($"创建要素类失败: {fcName}");
                                continue;
                            }

                            using (var fc = geodatabase.OpenDataset<FeatureClass>(fcName))
                            {
                                using (var insertCursor = fc.CreateInsertCursor())
                                {
                                    using (var buffer = fc.CreateRowBuffer())
                                    {
                                        foreach (var item in layerGroup)
                                        {
                                            buffer["GroupKey"] = item.GroupKey ?? "";
                                            buffer["LayerName"] = item.OverlayLayerName ?? "";
                                            // 根据用户设置的单位转换面积
                                            buffer[areaFieldName] = Math.Round(ConvertAreaUnit(item.Area, SelectedAreaUnit), DecimalPlaces);
                                            buffer[fc.GetDefinition().GetShapeField()] = item.Geometry;
                                            insertCursor.Insert(buffer);
                                        }
                                    }
                                    insertCursor.Flush();
                                }
                            }

                            LogInfo($"已导出: {fcName} ({layerGroup.Count()} 个要素)");
                            exportedCount++;
                        }
                        catch (Exception ex)
                        {
                            LogError($"导出 {layerName} 失败: {ex.Message}");
                        }
                    }
                }
            });

            LogInfo($"GDB导出完成，共导出 {exportedCount} 个图层");
            return exportedCount > 0 ? gdbPath : null;
        }

        private string GetAreaFieldName()
        {
            return SelectedAreaUnit switch
            {
                "平方米" => "Area_M2",
                "公顷" => "Area_Ha",
                "亩" => "Area_Mu",
                _ => "Area_M2"
            };
        }

        private string MakeSafeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Replace(" ", "_");
        }

        private void ExportToCsv(string filePath)
        {
            using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                var columnNames = ResultTable.Columns.Cast<DataColumn>().Select(c => $"\"{c.ColumnName}\"");
                writer.WriteLine(string.Join(",", columnNames));

                foreach (DataRow row in ResultTable.Rows)
                {
                    var values = row.ItemArray.Select(item =>
                        item is double d ? d.ToString($"F{DecimalPlaces}") : $"\"{item}\"");
                    writer.WriteLine(string.Join(",", values));
                }
            }
        }

        private void ExportToExcel(string filePath)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;

            try
            {
                excelApp = new Excel.Application { Visible = false, DisplayAlerts = false };
                workbook = excelApp.Workbooks.Add();
                worksheet = (Excel.Worksheet)workbook.Sheets[1];
                worksheet.Name = "多图层压盖汇总表";

                for (int col = 0; col < ResultTable.Columns.Count; col++)
                    worksheet.Cells[1, col + 1] = ResultTable.Columns[col].ColumnName;

                Excel.Range headerRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[1, ResultTable.Columns.Count]];
                headerRange.Font.Bold = true;
                headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(79, 129, 189));
                headerRange.Font.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.White);
                headerRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

                for (int row = 0; row < ResultTable.Rows.Count; row++)
                {
                    for (int col = 0; col < ResultTable.Columns.Count; col++)
                    {
                        var value = ResultTable.Rows[row][col];
                        if (value is double d)
                        {
                            worksheet.Cells[row + 2, col + 1] = Math.Round(d, DecimalPlaces);
                            ((Excel.Range)worksheet.Cells[row + 2, col + 1]).NumberFormat = DecimalPlaces > 0 ? $"0.{new string('0', DecimalPlaces)}" : "0";
                        }
                        else
                            worksheet.Cells[row + 2, col + 1] = value?.ToString() ?? "";
                    }
                }

                if (ResultTable.Rows.Count > 0)
                {
                    Excel.Range totalRowRange = worksheet.Range[
                        worksheet.Cells[ResultTable.Rows.Count + 1, 1],
                        worksheet.Cells[ResultTable.Rows.Count + 1, ResultTable.Columns.Count]];
                    totalRowRange.Font.Bold = true;
                    totalRowRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(221, 235, 247));
                }

                worksheet.Columns.AutoFit();

                Excel.Range dataRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[ResultTable.Rows.Count + 1, ResultTable.Columns.Count]];
                dataRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                dataRange.Borders.Weight = Excel.XlBorderWeight.xlThin;

                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook);
            }
            finally
            {
                if (workbook != null) { workbook.Close(false); System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook); }
                if (excelApp != null) { excelApp.Quit(); System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp); }
                if (worksheet != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private void ShowResultWindow()
        {
            if (ResultTable == null || ResultTable.Rows.Count == 0)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("没有可显示的数据", "提示");
                return;
            }

            try
            {
                var resultWindow = new MultiOverlaySummaryResultWindow(ResultTable, DecimalPlaces, _intersectGeometries, _spatialReference, SelectedAreaUnit);
                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LogError($"显示结果窗口失败: {ex.Message}");
            }
        }

        private void Cancel()
        {
            CancelRequested = true;
            StatusMessage = "正在取消操作...";
            LogWarning("用户请求取消操作");
        }

        private void ShowHelp()
        {
            var helpContent = "多图层压盖汇总工具使用说明\n\n" +
                "功能描述：\n" +
                "计算主图层与多个压盖图层的交集面积，\n" +
                "输出结果以图层名称作为列名，显示每个项目压占各图层的面积。\n\n" +
                "参数说明：\n" +
                "• 主图层：选择作为基础范围的面图层\n" +
                "• 唯一字段(可选)：选择用于分组的字段，不选则汇总全部\n" +
                "• 压盖图层：勾选需要计算压盖面积的图层（支持多选）\n\n" +
                "输出功能：\n" +
                "• 导出Excel/CSV：导出汇总表格\n" +
                "• 导出SHP：导出压盖部分的交集图形（按图层分别导出）\n\n" +
                "操作步骤：\n" +
                "1. 选择主图层\n" +
                "2. 可选：选择唯一字段进行分组\n" +
                "3. 勾选压盖图层\n" +
                "4. 点击\"开始\"执行计算\n" +
                "5. 查看结果或导出";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "多图层压盖汇总工具帮助");
        }

        #region 辅助方法

        private void UpdateProgress(bool isIndeterminate, int progress)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                IsProgressIndeterminate = isIndeterminate;
                Progress = progress;
            });
        }

        private void UpdateStatus(string message)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() => StatusMessage = message);
        }

        private void LogInfo(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            System.Windows.Application.Current?.Dispatcher?.Invoke(() => LogContent += logMessage + Environment.NewLine);
        }

        private void LogWarning(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] 警告: {message}";
            System.Windows.Application.Current?.Dispatcher?.Invoke(() => LogContent += logMessage + Environment.NewLine);
        }

        private void LogError(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] 错误: {message}";
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                LogContent += logMessage + Environment.NewLine;
                StatusMessage = $"错误: {message}";
            });
        }

        #endregion
    }
}
