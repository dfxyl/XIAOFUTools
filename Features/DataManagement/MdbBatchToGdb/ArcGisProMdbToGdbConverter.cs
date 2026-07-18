using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace XIAOFUTools.Features.DataManagement.MdbBatchToGdb
{
    internal enum BatchConversionEventKind
    {
        Started,
        Completed,
        Failed
    }

    internal readonly record struct BatchConversionEvent(int Index, BatchConversionEventKind Kind, string Message);

    internal sealed class ArcGisProMdbToGdbConverter
    {
        internal const string ConvertPersonalGeodatabaseTool = "conversion.ConvertPersonalGeodatabase";

        public async Task ConvertBatchAsync(
            IReadOnlyList<MdbConversionPlan> plans,
            Action<BatchConversionEvent>? onEvent,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            if (plans is null || plans.Count == 0)
            {
                return;
            }

            for (int index = 0; index < plans.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MdbConversionPlan plan = plans[index];

                try
                {
                    ValidatePlan(plan);
                    onEvent?.Invoke(new BatchConversionEvent(index, BatchConversionEventKind.Started, plan.Item.Name));
                    await ConvertAsync(plan, log, cancellationToken);
                    onEvent?.Invoke(new BatchConversionEvent(index, BatchConversionEventKind.Completed, plan.OutputPath));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    onEvent?.Invoke(new BatchConversionEvent(index, BatchConversionEventKind.Failed, ex.Message));
                }
            }
        }

        private static async Task ConvertAsync(
            MdbConversionPlan plan,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            string outputFolder = Path.GetDirectoryName(plan.OutputPath)
                ?? throw new DirectoryNotFoundException("无法识别输出目录。");
            string outputName = Path.GetFileNameWithoutExtension(plan.OutputPath);
            string outputFormat = plan.OutputFormat.GetGeoprocessingOutputFormat();
            log?.Invoke("调用 ArcGIS Pro 内置 Convert Personal Geodatabase。");
            log?.Invoke($"  输出格式：{outputFormat}");
            GPToolExecuteEventHandler eventHandler = (eventName, value) =>
            {
                if (eventName == "OnBeginExecute")
                {
                    log?.Invoke("  正在转换 MDB 数据...");
                    return;
                }

                if (eventName == "OnProgressMessage" && value is string operation && !string.IsNullOrWhiteSpace(operation))
                {
                    log?.Invoke($"  {operation}");
                    return;
                }

                if (eventName == "OnMessage" && value is IGPMessage message &&
                    (message.Type == GPMessageType.Warning || message.Type == GPMessageType.Error) &&
                    !string.IsNullOrWhiteSpace(message.Text))
                {
                    log?.Invoke($"  {message.Text}");
                }
            };
            IGPResult result = await Geoprocessing.ExecuteToolAsync(
                ConvertPersonalGeodatabaseTool,
                Geoprocessing.MakeValueArray(plan.Item.FullPath, outputFolder, outputName, outputFormat),
                null,
                cancellationToken,
                eventHandler,
                GPExecuteToolFlags.GPThread);

            if (!result.IsFailed)
            {
                log?.Invoke("  ArcGIS Pro 转换完成。");
                return;
            }

            string details = string.Join(
                "; ",
                result.Messages
                    .Select(message => message.Text)
                    .Where(message => !string.IsNullOrWhiteSpace(message)));
            string prefix = result.ErrorCode == 0
                ? $"ArcGIS Pro 工具执行失败: {ConvertPersonalGeodatabaseTool}"
                : $"ArcGIS Pro 工具执行失败: {ConvertPersonalGeodatabaseTool} ({result.ErrorCode})";
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(details)
                    ? prefix
                    : $"{prefix}。{details}");
        }

        private static void ValidatePlan(MdbConversionPlan plan)
        {
            if (plan.Item is null || string.IsNullOrWhiteSpace(plan.Item.FullPath) || !File.Exists(plan.Item.FullPath))
            {
                throw new FileNotFoundException("输入 MDB 不存在，请检查路径。", plan.Item?.FullPath);
            }

            if (string.IsNullOrWhiteSpace(plan.OutputPath))
            {
                throw new ArgumentException("输出路径不能为空。", nameof(plan));
            }

            string outputFolder = Path.GetDirectoryName(plan.OutputPath);
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new DirectoryNotFoundException("无法识别输出目录，请检查输出路径。");
            }

            Directory.CreateDirectory(outputFolder);
        }
    }
}
