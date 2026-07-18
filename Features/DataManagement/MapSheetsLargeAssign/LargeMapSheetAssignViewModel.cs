using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.MapSheetsLargeAssign
{
    internal class LargeMapSheetAssignViewModel : PropertyChangedBase
    {
        private FeatureLayer _selectedPolygonLayer;
        private FieldOption _selectedTargetField;
        private string _selectedScaleName = "1:2000/50*50";
        private string _namingConvention = "X-Y";
        private int _decimalPlaces = 3;
        private double _customWidth = 1000;
        private double _customHeight = 1000;
        private bool _isProcessing;
        private string _identifyResultText = "未查询";
        private readonly MapSheetIdentifySession _identifySession = new MapSheetIdentifySession();
        private string _logContent = string.Empty;
        private CancellationTokenSource _cancellationTokenSource;

        private readonly RelayCommand _refreshLayersCommand;
        private readonly RelayCommand _activateIdentifyCommand;
        private readonly RelayCommand _stopIdentifyCommand;
        private readonly RelayCommand _runCommand;
        private readonly RelayCommand _cancelCommand;
        private readonly RelayCommand _showHelpCommand;

        public LargeMapSheetAssignViewModel()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            TargetFields = new ObservableCollection<FieldOption>();
            ScaleNames = new ObservableCollection<string>(
                LargeScaleMapSheetCalculator.GetOptions().Select(option => option.Name).Concat(new[] { "自定义" }));
            NamingConventions = new ObservableCollection<string>(new[] { "X-Y", "Y-X" });

            _refreshLayersCommand = new RelayCommand(RefreshLayers, () => !IsProcessing);
            _activateIdentifyCommand = new RelayCommand(async () => await ActivateIdentifyAsync(), () => !IsProcessing && _identifySession.CanStart && TryCreateOption(out _));
            _stopIdentifyCommand = new RelayCommand(async () => await StopIdentifyAsync(), () => _identifySession.CanStop);
            _runCommand = new RelayCommand(async () => await RunAsync(), () => CanRun);
            _cancelCommand = new RelayCommand(Cancel, () => IsProcessing);
            _showHelpCommand = new RelayCommand(ShowHelp);

            RefreshLayers();
        }

        public ObservableCollection<FeatureLayer> PolygonLayers { get; }

        public ObservableCollection<FieldOption> TargetFields { get; }

        public ObservableCollection<string> ScaleNames { get; }

        public ObservableCollection<string> NamingConventions { get; }

        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                if (SetProperty(ref _selectedPolygonLayer, value))
                {
                    LoadTargetFields();
                    RaiseCommandStates();
                }
            }
        }

        public FieldOption SelectedTargetField
        {
            get => _selectedTargetField;
            set
            {
                if (SetProperty(ref _selectedTargetField, value))
                {
                    RaiseCommandStates();
                    NotifyPropertyChanged(() => CanRun);
                }
            }
        }

        public string SelectedScaleName
        {
            get => _selectedScaleName;
            set
            {
                if (SetProperty(ref _selectedScaleName, value))
                {
                    NotifyPropertyChanged(() => IsCustomSize);
                    RaiseCommandStates();
                    NotifyPropertyChanged(() => CanRun);
                }
            }
        }

        public bool IsCustomSize => string.Equals(SelectedScaleName, "自定义", StringComparison.Ordinal);

        public string NamingConvention
        {
            get => _namingConvention;
            set
            {
                if (SetProperty(ref _namingConvention, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set
            {
                if (SetProperty(ref _decimalPlaces, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public double CustomWidth
        {
            get => _customWidth;
            set
            {
                if (SetProperty(ref _customWidth, value))
                {
                    RaiseCommandStates();
                    NotifyPropertyChanged(() => CanRun);
                }
            }
        }

        public double CustomHeight
        {
            get => _customHeight;
            set
            {
                if (SetProperty(ref _customHeight, value))
                {
                    RaiseCommandStates();
                    NotifyPropertyChanged(() => CanRun);
                }
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    RaiseCommandStates();
                    NotifyPropertyChanged(() => CanRun);
                }
            }
        }

        public string IdentifyResultText
        {
            get => _identifyResultText;
            set => SetProperty(ref _identifyResultText, value);
        }

        public bool IsIdentifyModeActive => _identifySession.IsActive;

        public string IdentifyStatusText => _identifySession.StatusText;

        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        public bool CanRun => !IsProcessing &&
                              SelectedPolygonLayer != null &&
                              SelectedTargetField != null &&
                              TryCreateOption(out _);

        public RelayCommand RefreshLayersCommand => _refreshLayersCommand;

        public RelayCommand ActivateIdentifyCommand => _activateIdentifyCommand;

        public RelayCommand StopIdentifyCommand => _stopIdentifyCommand;

        public RelayCommand RunCommand => _runCommand;

        public RelayCommand CancelCommand => _cancelCommand;

        public RelayCommand ShowHelpCommand => _showHelpCommand;

        private async void RefreshLayers()
        {
            try
            {
                var layers = await QueuedTask.Run(() => LayerFieldSelectionUtils.GetPolygonLayers());
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    var previousName = SelectedPolygonLayer?.Name;
                    PolygonLayers.Clear();
                    foreach (var layer in layers)
                    {
                        PolygonLayers.Add(layer);
                    }

                    SelectedPolygonLayer = PolygonLayers.FirstOrDefault(layer => string.Equals(layer.Name, previousName, StringComparison.Ordinal)) ??
                                           PolygonLayers.FirstOrDefault();
                });
            }
            catch (Exception ex)
            {
                AppendLog($"刷新图层失败: {ex.Message}");
            }
        }

        private async void LoadTargetFields()
        {
            try
            {
                if (SelectedPolygonLayer == null)
                {
                    TargetFields.Clear();
                    SelectedTargetField = null;
                    return;
                }

                var fields = await QueuedTask.Run(() => LayerFieldSelectionUtils.GetWritableTextFields(SelectedPolygonLayer));
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    var previousName = SelectedTargetField?.Name;
                    TargetFields.Clear();
                    foreach (var field in fields)
                    {
                        TargetFields.Add(field);
                    }

                    SelectedTargetField = TargetFields.FirstOrDefault(field => string.Equals(field.Name, previousName, StringComparison.Ordinal)) ??
                                          TargetFields.FirstOrDefault();
                });
            }
            catch (Exception ex)
            {
                AppendLog($"加载字段失败: {ex.Message}");
            }
        }

        private async Task ActivateIdentifyAsync()
        {
            if (!TryCreateOption(out var option))
            {
                PresentationServices.Dialogs.Show("请先设置有效的大比例图幅尺寸。", "提示");
                return;
            }

            LargeMapSheetIdentifyTool.Configure(option, NamingConvention, DecimalPlaces);
            LargeMapSheetIdentifyTool.ResultHandler = OnIdentifyCompleted;
            LargeMapSheetIdentifyTool.ActiveStateHandler = OnIdentifyStateChanged;
            AppendLog("进入地图查询模式，单击地图可查询大比例图幅。");
            await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_LargeMapSheetIdentifyTool");
        }

        private async Task StopIdentifyAsync()
        {
            await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
            OnIdentifyStateChanged(false);
            AppendLog("已退出大比例图幅查询模式。");
        }

        private async Task RunAsync()
        {
            if (!TryCreateOption(out var option))
            {
                PresentationServices.Dialogs.Show("请先设置有效的大比例图幅尺寸。", "提示");
                return;
            }

            if (SelectedPolygonLayer == null || SelectedTargetField == null)
            {
                PresentationServices.Dialogs.Show("请选择面图层和目标字段。", "提示");
                return;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            IsProcessing = true;
            AppendLog($"开始写入大比例图幅，图层: {SelectedPolygonLayer.Name}，字段: {SelectedTargetField.Name}");

            try
            {
                var summary = await QueuedTask.Run(() => ExecuteAssignment(option, _cancellationTokenSource.Token));
                AppendLog($"写入完成，共处理 {summary.ProcessedCount} 个图斑，更新 {summary.UpdatedCount} 条记录。");
            }
            catch (OperationCanceledException)
            {
                AppendLog("操作已取消。");
            }
            catch (Exception ex)
            {
                AppendLog($"运行失败: {ex.Message}");
                PresentationServices.Dialogs.Show($"大比例图幅赋值失败: {ex.Message}", "错误");
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private AssignmentSummary ExecuteAssignment(LargeScaleMapSheetOption option, CancellationToken cancellationToken)
        {
            var updatedCount = 0;
            var processedCount = 0;
            using var table = SelectedPolygonLayer.GetTable();
            using var cursor = table.Search(null, false);
            var editOperation = new EditOperation { Name = "大比例图幅赋值" };

            while (cursor.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var feature = cursor.Current as Feature;
                if (feature == null)
                {
                    continue;
                }

                processedCount++;
                var geometry = feature.GetShape();
                var value = geometry == null || geometry.IsEmpty
                    ? string.Empty
                    : string.Join("、", MapSheetGeometryService.GetLargeScaleCodes(geometry, option, NamingConvention, DecimalPlaces));

                ValidateFieldLength(value, feature.GetObjectID());
                if (!MapSheetAssignmentUtils.ShouldUpdateValue(feature[SelectedTargetField.Name], value))
                {
                    continue;
                }

                editOperation.Modify(table, feature.GetObjectID(), new Dictionary<string, object>
                {
                    { SelectedTargetField.Name, value }
                });
                updatedCount++;
            }

            if (updatedCount > 0 && !editOperation.Execute())
            {
                throw new InvalidOperationException("编辑操作执行失败。");
            }

            return new AssignmentSummary(processedCount, updatedCount);
        }

        private void ValidateFieldLength(string value, long objectId)
        {
            if (SelectedTargetField == null || SelectedTargetField.Length <= 0 || string.IsNullOrEmpty(value))
            {
                return;
            }

            if (value.Length > SelectedTargetField.Length)
            {
                throw new InvalidOperationException(
                    $"字段 {SelectedTargetField.Name} 长度不足，OID={objectId} 需要 {value.Length} 个字符，但字段长度只有 {SelectedTargetField.Length}。");
            }
        }

        private void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        private void ShowHelp()
        {
            PresentationServices.Dialogs.Show(
                "1. 选择面图层和文本字段。\n2. 选择大比例图幅规格，多个图幅会用“、”连接后写入字段。\n3. 点击“开始查询”可在地图上单击查询当前规格对应的图幅，并高亮边框。",
                "大比例图幅赋值");
        }

        private void OnIdentifyCompleted(MapSheetIdentifyResult result)
        {
            IdentifyResultText = result.DisplayText;
            AppendLog($"查询结果: {result.DisplayText}");
        }

        private void OnIdentifyStateChanged(bool isActive)
        {
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                if (isActive)
                {
                    _identifySession.Start();
                }
                else
                {
                    _identifySession.Stop();
                }

                NotifyPropertyChanged(() => IsIdentifyModeActive);
                NotifyPropertyChanged(() => IdentifyStatusText);
                RaiseCommandStates();
            });
        }

        private bool TryCreateOption(out LargeScaleMapSheetOption option)
        {
            if (!IsCustomSize)
            {
                option = LargeScaleMapSheetCalculator.GetOption(SelectedScaleName);
                return true;
            }

            if (CustomWidth > 0 && CustomHeight > 0)
            {
                option = new LargeScaleMapSheetOption("自定义", CustomWidth, CustomHeight);
                return true;
            }

            option = default;
            return false;
        }

        private void RaiseCommandStates()
        {
            _refreshLayersCommand.RaiseCanExecuteChanged();
            _activateIdentifyCommand.RaiseCanExecuteChanged();
            _stopIdentifyCommand.RaiseCanExecuteChanged();
            _runCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
        }

        private void AppendLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent = string.IsNullOrWhiteSpace(LogContent)
                ? $"[{timestamp}] {message}"
                : $"{LogContent}{Environment.NewLine}[{timestamp}] {message}";
        }

        private readonly record struct AssignmentSummary(int ProcessedCount, int UpdatedCount);
    }
}
