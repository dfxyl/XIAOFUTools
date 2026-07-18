using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure
{
    internal sealed class OvertureMfcGenerator : IOvertureMfcGenerator
    {
        private readonly IOvertureMfcDuckDbInspector _inspector;
        private readonly IOvertureMfcJsonWriter _writer;

        public OvertureMfcGenerator(
            IOvertureMfcDuckDbInspector inspector,
            IOvertureMfcJsonWriter writer)
        {
            _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public async Task<bool> GenerateAsync(
            string sourceDataFolder,
            string outputMfcFilePath,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceDataFolder);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputMfcFilePath);
            log ??= Console.WriteLine;

            var sourceFolder = Path.GetFullPath(sourceDataFolder);
            log($"开始生成 MFC。源: {sourceFolder}, 输出: {outputMfcFilePath}");
            var datasetDirectories = Directory.GetDirectories(sourceFolder)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (datasetDirectories.Length == 0)
            {
                log($"在 {sourceFolder} 中未找到数据集子文件夹。无法生成 MFC。");
                return false;
            }

            await _inspector.InitializeAsync(cancellationToken).ConfigureAwait(false);
            var definition = new OvertureMfcDefinition
            {
                Connection = new OvertureMfcConnection
                {
                    Properties = new OvertureMfcConnectionProperties
                    {
                        Path = sourceFolder.Replace('/', '\\')
                    }
                }
            };

            foreach (var datasetDirectory in datasetDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var datasetName = Path.GetFileName(datasetDirectory);
                log($"Processing dataset: {datasetName}");
                var parquetFiles = Directory.GetFiles(datasetDirectory, "*.parquet")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (parquetFiles.Length == 0)
                {
                    log(
                        $"No .parquet files found in {datasetDirectory} for dataset " +
                        $"{datasetName}. Skipping.");
                    continue;
                }

                try
                {
                    var inspection = await _inspector.InspectDatasetAsync(
                        datasetName,
                        parquetFiles,
                        cancellationToken).ConfigureAwait(false);
                    var dataset = OvertureMfcSchemaPlanner.PlanDataset(
                        datasetName,
                        inspection.Columns,
                        inspection.GeometryType,
                        log);
                    definition.Datasets.Add(dataset);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    log(
                        $"Error describing schema for {parquetFiles[0]} in dataset " +
                        $"{datasetName}: {exception.Message}");
                }
            }

            await _writer.WriteAsync(
                definition,
                outputMfcFilePath,
                cancellationToken).ConfigureAwait(false);
            log($"MFC 文件已成功生成于 {outputMfcFilePath}");
            return true;
        }
    }
}
