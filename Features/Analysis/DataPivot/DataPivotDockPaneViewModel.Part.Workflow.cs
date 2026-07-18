using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.DataPivot
{
    internal partial class DataPivotDockPaneViewModel
    {

        private async Task ExecuteAsync()
        {
            if (!CanProcess)
            {
                StatusMessage = "请先完善输入参数。";
                return;
            }

            string tempTable = null;
            string summaryTable = null;

            try
            {
                IsProcessing = true;
                IsProgressIndeterminate = false;
                Progress = 0;
                StatusMessage = "正在执行数据透视...";
                LogContent = string.Empty;

                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                var regionFields = RegionFields.Where(f => f.IsSelected).Select(f => f.FieldName).ToList();
                var regionFieldsText = string.Join(";", regionFields);
                var statsType = GetStatisticsType(SelectedAggregationType);
                var statsOutputField = $"{statsType}_{TempValueField}";
                var caseFields = string.Join(";", regionFields.Concat(new[] { TempPivotField }));
                var outputPath = OutputTablePath;
                var tempWorkspace = ResolveTemporaryWorkspacePath();

                if (!_workspaceResolver.DirectoryExists(tempWorkspace))
                {
                    LogError("临时工作空间不可用，请检查输出数据库或项目默认数据库。");
                    return;
                }

                var runId = DateTime.Now.ToString("yyyyMMddHHmmssfff");

                tempTable = Path.Combine(tempWorkspace, $"XFT_PivotTemp_{runId}");
                summaryTable = Path.Combine(tempWorkspace, $"XFT_PivotSummary_{runId}");

                LogInfo($"输入表: {SelectedInputDataset.Name}");
                LogInfo($"区域字段: {regionFieldsText}");
                LogInfo($"透视字段: {SelectedPivotField.FieldName}");
                LogInfo($"数值字段: {SelectedValueField.FieldName}");
                LogInfo($"汇总方式: {SelectedAggregationType}");
                LogInfo($"输出表: {outputPath}");
                LogInfo($"临时工作空间: {tempWorkspace}");

                var envNoMap = Geoprocessing.MakeEnvironmentArray("overwriteoutput", "True", "addOutputsToMap", "False");
                var envAddToMap = Geoprocessing.MakeEnvironmentArray("overwriteoutput", "True", "addOutputsToMap", "True");

                Progress = 10;
                StatusMessage = "正在复制输入表...";
                var copyRowsParams = Geoprocessing.MakeValueArray(SelectedInputDataset.ToGeoprocessingInput(), tempTable);
                var copyRowsResult = await Geoprocessing.ExecuteToolAsync("management.CopyRows", copyRowsParams, envNoMap, _cts.Token, null, GPExecuteToolFlags.GPThread);
                if (!HandleGpResult(copyRowsResult, "复制输入表失败"))
                    return;

                Progress = 25;
                StatusMessage = "正在准备透视字段...";
                var addPivotFieldParams = Geoprocessing.MakeValueArray(tempTable, TempPivotField, "TEXT", null, null, 255, "透视字段");
                var addPivotFieldResult = await Geoprocessing.ExecuteToolAsync("management.AddField", addPivotFieldParams, envNoMap, _cts.Token, null, GPExecuteToolFlags.GPThread);
                if (!HandleGpResult(addPivotFieldResult, "创建临时透视字段失败"))
                    return;

                var pivotCodeBlock =
                    "def pivot_key(value):\n" +
                    "    if value is None:\n" +
                    "        return \"空值\"\n" +
                    "    text = str(value).strip()\n" +
                    "    return text if text != \"\" else \"空值\"\n";
                var calcPivotFieldParams = Geoprocessing.MakeValueArray(
                    tempTable,
                    TempPivotField,
                    $"pivot_key(!{SelectedPivotField.FieldName}!)",
                    "PYTHON3",
                    pivotCodeBlock);
                var calcPivotFieldResult = await Geoprocessing.ExecuteToolAsync("management.CalculateField", calcPivotFieldParams, envNoMap, _cts.Token, null, GPExecuteToolFlags.GPThread);
                if (!HandleGpResult(calcPivotFieldResult, "计算透视字段失败"))
                    return;

                Progress = 45;
                StatusMessage = "正在准备数值字段...";
                var addValueFieldParams = Geoprocessing.MakeValueArray(tempTable, TempValueField, "DOUBLE", null, null, null, "透视数值");
                var addValueFieldResult = await Geoprocessing.ExecuteToolAsync("management.AddField", addValueFieldParams, envNoMap, _cts.Token, null, GPExecuteToolFlags.GPThread);
                if (!HandleGpResult(addValueFieldResult, "创建临时数值字段失败"))
                    return;

                var valueCodeBlock =
                    "def to_num(value):\n" +
                    "    try:\n" +
                    "        if value is None:\n" +
                    "            return 0\n" +
                    "        text = str(value).strip()\n" +
                    "        if text == \"\":\n" +
                    "            return 0\n" +
                    "        return float(text)\n" +
                    "    except:\n" +
                    "        return 0\n";
                var calcValueFieldParams = Geoprocessing.MakeValueArray(
                    tempTable,
                    TempValueField,
                    $"to_num(!{SelectedValueField.FieldName}!)",
                    "PYTHON3",
                    valueCodeBlock);
                var calcValueFieldResult = await Geoprocessing.ExecuteToolAsync("management.CalculateField", calcValueFieldParams, envNoMap, _cts.Token, null, GPExecuteToolFlags.GPThread);
                if (!HandleGpResult(calcValueFieldResult, "计算数值字段失败"))
                    return;

                Progress = 65;
                StatusMessage = "正在汇总统计...";
                var statisticsParams = Geoprocessing.MakeValueArray(tempTable, summaryTable, $"{TempValueField} {statsType}", caseFields);
                var statisticsResult = await Geoprocessing.ExecuteToolAsync("analysis.Statistics", statisticsParams, envNoMap, _cts.Token, null, GPExecuteToolFlags.GPThread);
                if (!HandleGpResult(statisticsResult, "统计汇总失败"))
                    return;

                Progress = 85;
                StatusMessage = "正在生成透视表...";
                var pivotTableParams = Geoprocessing.MakeValueArray(summaryTable, regionFieldsText, TempPivotField, statsOutputField, outputPath);
                var pivotTableResult = await Geoprocessing.ExecuteToolAsync("management.PivotTable", pivotTableParams, envAddToMap, _cts.Token, null, GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddOutputsToMap);
                if (!HandleGpResult(pivotTableResult, "创建透视表失败"))
                    return;

                Progress = 100;
                StatusMessage = "数据透视完成。";
                LogInfo("数据透视执行成功。");
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消。";
                LogWarning("用户取消了数据透视操作。");
            }
            catch (Exception ex)
            {
                LogError($"执行失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                await DeleteTempTableAsync(summaryTable);
                await DeleteTempTableAsync(tempTable);

                _cts?.Dispose();
                _cts = null;
            }
        }

        private bool HandleGpResult(IGPResult result, string failedMessage)
        {
            if (result == null)
            {
                LogError(failedMessage);
                return false;
            }

            foreach (var message in result.Messages)
            {
                if (!string.IsNullOrWhiteSpace(message.Text))
                    LogInfo(message.Text);
            }

            if (result.IsFailed)
            {
                LogError(failedMessage);
                return false;
            }

            return true;
        }
    }
}
