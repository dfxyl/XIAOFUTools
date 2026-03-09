using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.SpecialCoordinateTransform
{
    internal sealed partial class SpecialCoordinateTransformDockPaneViewModel
    {
        private async Task RunTransformAsync()
        {
            if (!CanProcess)
            {
                return;
            }

            IsProcessing = true;
            ResetItemStatuses(BatchItems);
            ResetItemStatuses(GdbItems);
            UpdateProgress(0);
            SetProgressIndeterminate(true);
            SetStatus("正在处理...");

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                switch (SelectedMode)
                {
                    case TransformMode.SingleLayer:
                        await QueuedTask.Run(async () => await ExecuteSingleLayerModeAsync(_cancellationTokenSource.Token));
                        break;
                    case TransformMode.BatchShapefile:
                        await QueuedTask.Run(async () => await ExecuteBatchShapefileModeAsync(_cancellationTokenSource.Token));
                        break;
                    case TransformMode.FileGeodatabase:
                        await QueuedTask.Run(async () => await ExecuteBatchGdbModeAsync(_cancellationTokenSource.Token));
                        break;
                }

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    SetStatus("转换完成");
                    AddLog("转换完成。");
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("操作已取消");
                AddLog("操作已取消。");
            }
            catch (Exception ex)
            {
                SetStatus($"转换失败: {ex.Message}");
                AddLog($"错误: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                SetProgressIndeterminate(false);
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task ExecuteSingleLayerModeAsync(CancellationToken cancellationToken)
        {
            if (SelectedInputLayer is not FeatureLayer featureLayer)
            {
                throw new InvalidOperationException("未选择有效的输入图层。");
            }

            string outputName = $"{featureLayer.Name}_{GetConversionShortName(SelectedConversionType.Key)}";
            var plan = new DatasetTransformPlan
            {
                DisplayName = featureLayer.Name,
                OutputPath = OutputPath,
                DefaultOutputName = outputName,
                TemplateValue = featureLayer,
                SourceKind = InputSourceKind.MapLayer,
                FeatureLayer = featureLayer
            };

            await TransformDatasetAsync(plan, cancellationToken, true);
        }

        private async Task ExecuteBatchShapefileModeAsync(CancellationToken cancellationToken)
        {
            List<DatasetTransformPlan> plans = BuildShapefilePlans();
            if (plans.Count == 0)
            {
                throw new InvalidOperationException("批量模式没有选中任何 SHP。");
            }

            SetProgressIndeterminate(false);
            UpdateProgress(0);

            int completed = 0;
            foreach (DatasetTransformPlan plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                UpdateItemStatus(plan.OwnerItem, "处理中");
                SetStatus($"正在处理 {completed + 1}/{plans.Count}: {plan.DisplayName}");

                try
                {
                    await TransformDatasetAsync(plan, cancellationToken, false);
                    UpdateItemStatus(plan.OwnerItem, "完成");
                }
                catch (OperationCanceledException)
                {
                    UpdateItemStatus(plan.OwnerItem, "已取消");
                    throw;
                }
                catch (Exception ex)
                {
                    UpdateItemStatus(plan.OwnerItem, "失败");
                    AddLog($"{plan.DisplayName} 转换失败: {ex.Message}");
                }
                finally
                {
                    completed++;
                    UpdateProgress((double)completed / plans.Count * 100.0);
                }
            }
        }

        private async Task ExecuteBatchGdbModeAsync(CancellationToken cancellationToken)
        {
            List<GdbTransformPlan> gdbPlans = BuildGdbPlans();
            int totalFeatureClasses = gdbPlans.Sum(plan => plan.DatasetPlans.Count);
            if (totalFeatureClasses == 0)
            {
                throw new InvalidOperationException("选中的 GDB 中没有可转换的要素类。");
            }

            SetProgressIndeterminate(false);
            UpdateProgress(0);

            int completed = 0;
            foreach (GdbTransformPlan gdbPlan in gdbPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                UpdateItemStatus(gdbPlan.OwnerItem, "处理中");

                try
                {
                    await RecreateOutputGdbAsync(gdbPlan.OutputGdbPath);
                }
                catch (Exception ex)
                {
                    UpdateItemStatus(gdbPlan.OwnerItem, "失败");
                    AddLog($"{gdbPlan.OwnerItem.Name} 输出库准备失败: {ex.Message}");
                    completed += gdbPlan.DatasetPlans.Count;
                    UpdateProgress((double)completed / totalFeatureClasses * 100.0);
                    continue;
                }

                bool hasFailure = false;
                foreach (DatasetTransformPlan datasetPlan in gdbPlan.DatasetPlans)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SetStatus($"正在处理 {gdbPlan.OwnerItem.Name}: {datasetPlan.DisplayName}");

                    try
                    {
                        await TransformDatasetAsync(datasetPlan, cancellationToken, false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        hasFailure = true;
                        AddLog($"{gdbPlan.OwnerItem.Name} -> {datasetPlan.DisplayName} 失败: {ex.Message}");
                    }
                    finally
                    {
                        completed++;
                        UpdateProgress((double)completed / totalFeatureClasses * 100.0);
                    }
                }

                UpdateItemStatus(gdbPlan.OwnerItem, hasFailure ? "部分失败" : "完成");
            }
        }

        private List<DatasetTransformPlan> BuildShapefilePlans()
        {
            var selectedItems = BatchItems.Where(item => item.IsSelected).ToList();
            var plans = new List<DatasetTransformPlan>(selectedItems.Count);

            foreach (BatchTransformItem item in selectedItems)
            {
                string sourceFolder = Path.GetDirectoryName(item.FullPath) ?? BatchInputFolderPath;
                string outputFolder = BatchSaveToSourceFolder
                    ? sourceFolder
                    : GetMirroredOutputFolder(BatchInputFolderPath, sourceFolder, BatchOutputFolderPath);

                string outputName = $"{Path.GetFileNameWithoutExtension(item.FullPath)}_{GetConversionShortName(SelectedConversionType.Key)}";
                plans.Add(new DatasetTransformPlan
                {
                    OwnerItem = item,
                    DisplayName = item.RelativePath,
                    OutputPath = Path.Combine(outputFolder, outputName + ".shp"),
                    DefaultOutputName = outputName,
                    TemplateValue = item.FullPath,
                    SourceKind = InputSourceKind.Shapefile,
                    ShapefilePath = item.FullPath
                });
            }

            return plans;
        }

        private List<GdbTransformPlan> BuildGdbPlans()
        {
            var selectedItems = GdbItems.Where(item => item.IsSelected).ToList();
            var plans = new List<GdbTransformPlan>(selectedItems.Count);

            foreach (BatchTransformItem item in selectedItems)
            {
                string inputParent = Path.GetDirectoryName(item.FullPath) ?? GdbInputFolderPath;
                string outputParent = GdbSaveToSourceFolder
                    ? inputParent
                    : GetMirroredOutputFolder(GdbInputFolderPath, inputParent, GdbOutputFolderPath);

                string outputGdbPath = Path.Combine(
                    outputParent,
                    $"{Path.GetFileNameWithoutExtension(item.FullPath)}_{GetConversionShortName(SelectedConversionType.Key)}.gdb");

                plans.Add(new GdbTransformPlan
                {
                    OwnerItem = item,
                    OutputGdbPath = outputGdbPath,
                    DatasetPlans = DiscoverGdbDatasetPlans(item, outputGdbPath)
                });
            }

            return plans;
        }

        private List<DatasetTransformPlan> DiscoverGdbDatasetPlans(BatchTransformItem ownerItem, string outputGdbPath)
        {
            var plans = new List<DatasetTransformPlan>();
            var datasetFeatureClassNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(ownerItem.FullPath)));

            foreach (FeatureDatasetDefinition datasetDefinition in geodatabase.GetDefinitions<FeatureDatasetDefinition>())
            {
                string datasetName = datasetDefinition.GetName();
                using var featureDataset = geodatabase.OpenDataset<FeatureDataset>(datasetName);

                foreach (FeatureClassDefinition featureClassDefinition in featureDataset.GetDefinitions<FeatureClassDefinition>())
                {
                    string featureClassName = featureClassDefinition.GetName();
                    string relativePath = Path.Combine(datasetName, featureClassName);
                    datasetFeatureClassNames.Add(featureClassName);

                    plans.Add(new DatasetTransformPlan
                    {
                        OwnerItem = ownerItem,
                        DisplayName = relativePath,
                        OutputPath = Path.Combine(outputGdbPath, relativePath),
                        DefaultOutputName = featureClassName,
                        TemplateValue = Path.Combine(ownerItem.FullPath, relativePath),
                        SourceKind = InputSourceKind.FileGeodatabase,
                        InputGdbPath = ownerItem.FullPath,
                        RelativePathInGdb = relativePath,
                        FeatureDatasetName = datasetName
                    });
                }
            }

            foreach (FeatureClassDefinition featureClassDefinition in geodatabase.GetDefinitions<FeatureClassDefinition>())
            {
                string featureClassName = featureClassDefinition.GetName();
                if (datasetFeatureClassNames.Contains(featureClassName))
                {
                    continue;
                }

                plans.Add(new DatasetTransformPlan
                {
                    OwnerItem = ownerItem,
                    DisplayName = featureClassName,
                    OutputPath = Path.Combine(outputGdbPath, featureClassName),
                    DefaultOutputName = featureClassName,
                    TemplateValue = Path.Combine(ownerItem.FullPath, featureClassName),
                    SourceKind = InputSourceKind.FileGeodatabase,
                    InputGdbPath = ownerItem.FullPath,
                    RelativePathInGdb = featureClassName
                });
            }

            return plans;
        }

        private async Task TransformDatasetAsync(DatasetTransformPlan plan, CancellationToken cancellationToken, bool reportFeatureProgress)
        {
            AddLog($"开始转换: {plan.DisplayName}");
            AddLog($"输出位置: {plan.OutputPath}");
            AddLog($"转换类型: {SelectedConversionType.DisplayName}");

            using DatasetHandle inputHandle = OpenInputDataset(plan);
            FeatureClass inputFeatureClass = inputHandle.FeatureClass ?? throw new InvalidOperationException("无法打开输入要素类。");
            FeatureClassDefinition definition = inputFeatureClass.GetDefinition();

            using DatasetHandle outputHandle = await CreateOutputDatasetAsync(
                plan.OutputPath,
                plan.DefaultOutputName,
                plan.TemplateValue,
                definition,
                plan.FeatureDatasetName);

            await TransformFeaturesAsync(inputFeatureClass, outputHandle.FeatureClass, SelectedConversionType.Key, cancellationToken, reportFeatureProgress);
        }

        private DatasetHandle OpenInputDataset(DatasetTransformPlan plan)
        {
            return plan.SourceKind switch
            {
                InputSourceKind.MapLayer => OpenLayerDataset(plan.FeatureLayer),
                InputSourceKind.Shapefile => OpenShapefileDataset(plan.ShapefilePath),
                InputSourceKind.FileGeodatabase => OpenGdbDataset(plan.InputGdbPath, plan.RelativePathInGdb),
                _ => throw new InvalidOperationException("不支持的输入类型。")
            };
        }
    }
}
