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

namespace XIAOFUTools.Features.DataManagement.MapSheetsSmallAssign
{
    internal class SmallMapSheetAssignViewModel : PropertyChangedBase
    {
        private FeatureLayer _selectedPolygonLayer;
        private FieldOption _selectedTargetField;
        private string _selectedScaleName = "10万";
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

        public SmallMapSheetAssignViewModel()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            TargetFields = new ObservableCollection<FieldOption>();
            ScaleNames = new ObservableCollection<string>(SmallScaleMapSheetCalculator.GetOptions().Select(option => option.Name));

            _refreshLayersCommand = new RelayCommand(RefreshLayers, () => !IsProcessing);
            _activateIdentifyCommand = new RelayCommand(async () => await ActivateIdentifyAsync(), () => !IsProcessing && _identifySession.CanStart);
            _stopIdentifyCommand = new RelayCommand(async () => await StopIdentifyAsync(), () => _identifySession.CanStop);
            _runCommand = new RelayCommand(async () => await RunAsync(), () => CanRun);
            _cancelCommand = new RelayCommand(Cancel, () => IsProcessing);
            _showHelpCommand = new RelayCommand(ShowHelp);

            RefreshLayers();
        }

        public ObservableCollection<FeatureLayer> PolygonLayers { get; }

        public ObservableCollection<FieldOption> TargetFields { get; }

        public ObservableCollection<string> ScaleNames { get; }

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
                    RaiseCommandStates();
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

        public bool CanRun => !IsProcessing && SelectedPolygonLayer != null && SelectedTargetField != null;

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
            SmallMapSheetIdentifyTool.Configure(SmallScaleMapSheetCalculator.GetOption(SelectedScaleName));
            SmallMapSheetIdentifyTool.ResultHandler = OnIdentifyCompleted;
            SmallMapSheetIdentifyTool.ActiveStateHandler = OnIdentifyStateChanged;
            AppendLog("进入地图查询模式，单击地图可查询小比例图幅。");
            await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_SmallMapSheetIdentifyTool");
        }

        private async Task StopIdentifyAsync()
        {
            await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
            OnIdentifyStateChanged(false);
            AppendLog("已退出小比例图幅查询模式。");
        }

        private async Task RunAsync()
        {
            if (SelectedPolygonLayer == null || SelectedTargetField == null)
            {
                PresentationServices.Dialogs.Show("请选择面图层和目标字段。", "提示");
                return;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            IsProcessing = true;
            AppendLog($"开始写入小比例图幅，图层: {SelectedPolygonLayer.Name}，字段: {SelectedTargetField.Name}");

            try
            {
                var option = SmallScaleMapSheetCalculator.GetOption(SelectedScaleName);
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
                PresentationServices.Dialogs.Show($"小比例图幅赋值失败: {ex.Message}", "错误");
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private AssignmentSummary ExecuteAssignment(SmallScaleMapSheetOption option, CancellationToken cancellationToken)
        {
            var updatedCount = 0;
            var processedCount = 0;
            using var table = SelectedPolygonLayer.GetTable();
            using var cursor = table.Search(null, false);
            var editOperation = new EditOperation { Name = "小比例图幅赋值" };

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
                    : string.Join("、", MapSheetGeometryService.GetSmallScaleCodes(geometry, option));

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
                "1. 选择面图层和文本字段。\n2. 选择小比例尺规格，多个图幅会用“、”连接后写入字段。\n3. 点击“开始查询”可在地图上单击查询当前比例尺对应的图幅，并高亮边框。",
                "小比例图幅赋值");
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
