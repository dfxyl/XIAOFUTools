using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
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
        private const int MaxErrorLines = 80;

        public void Convert(string inputMdb, string outputGdb, Action<string>? log, CancellationToken cancellationToken)
        {
            ConvertBatch(
                new[] { new MdbConversionPlan(new MdbFileItem { FullPath = inputMdb, Name = Path.GetFileNameWithoutExtension(inputMdb) }, outputGdb) },
                onEvent: null,
                log,
                cancellationToken);
        }

        public void ConvertBatch(
            IReadOnlyList<MdbConversionPlan> plans,
            Action<BatchConversionEvent>? onEvent,
            Action<string>? log,
            CancellationToken cancellationToken)
        {
            if (plans == null || plans.Count == 0)
            {
                return;
            }

            foreach (MdbConversionPlan plan in plans)
            {
                if (string.IsNullOrWhiteSpace(plan.Item?.FullPath) || !File.Exists(plan.Item.FullPath))
                {
                    throw new FileNotFoundException("输入 MDB 不存在，请检查路径。", plan.Item?.FullPath);
                }

                if (string.IsNullOrWhiteSpace(plan.OutputPath))
                {
                    throw new ArgumentException("输出 GDB 路径不能为空。", nameof(plans));
                }

                string outputFolder = Path.GetDirectoryName(plan.OutputPath)
                    ?? throw new DirectoryNotFoundException("无法识别输出目录，请检查输出路径。");
                Directory.CreateDirectory(outputFolder);
            }

            cancellationToken.ThrowIfCancellationRequested();

            ArcGisProPythonEnvironment environment = ArcGisProPythonEnvironment.Resolve();
            log?.Invoke($"使用 ArcGIS Pro 自带 Python: {environment.InstallRoot}");

            string manifestPath = WriteManifest(plans);
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = environment.PropyPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                startInfo.ArgumentList.Add(environment.ScriptPath);
                startInfo.ArgumentList.Add("--manifest");
                startInfo.ArgumentList.Add(manifestPath);
                startInfo.Environment["PYTHONUTF8"] = "1";

                using var process = new Process { StartInfo = startInfo };
                var errorLines = new ConcurrentQueue<string>();

                process.OutputDataReceived += (_, args) =>
                {
                    if (string.IsNullOrWhiteSpace(args.Data))
                    {
                        return;
                    }

                    HandleOutputLine(args.Data, onEvent, log, errorLines);
                };

                process.ErrorDataReceived += (_, args) =>
                {
                    if (string.IsNullOrWhiteSpace(args.Data))
                    {
                        return;
                    }

                    EnqueueError(errorLines, args.Data);
                };

                try
                {
                    if (!process.Start())
                    {
                        throw new InvalidOperationException("启动 ArcGIS Pro Python 转换进程失败。");
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"启动 ArcGIS Pro Python 转换进程失败: {ex.Message}", ex);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using CancellationTokenRegistration registration = cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                    }
                });

                process.WaitForExit();
                cancellationToken.ThrowIfCancellationRequested();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(BuildErrorMessage(process.ExitCode, errorLines));
                }

                foreach (MdbConversionPlan plan in plans)
                {
                    if (!Directory.Exists(plan.OutputPath))
                    {
                        throw new InvalidOperationException($"转换进程已结束，但未找到输出 GDB: {plan.OutputPath}");
                    }
                }
            }
            finally
            {
                TryDeleteManifest(manifestPath);
            }
        }

        private static string WriteManifest(IReadOnlyList<MdbConversionPlan> plans)
        {
            string manifestPath = Path.Combine(
                Path.GetTempPath(),
                $"xft_mdb_to_gdb_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json");

            var jobs = new List<object>(plans.Count);
            for (int i = 0; i < plans.Count; i++)
            {
                jobs.Add(new
                {
                    index = i,
                    input = plans[i].Item.FullPath,
                    output = plans[i].OutputPath
                });
            }

            File.WriteAllText(manifestPath, JsonSerializer.Serialize(jobs), new UTF8Encoding(false));
            return manifestPath;
        }

        private static void TryDeleteManifest(string manifestPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
                {
                    File.Delete(manifestPath);
                }
            }
            catch
            {
            }
        }

        private static void HandleOutputLine(
            string line,
            Action<BatchConversionEvent>? onEvent,
            Action<string>? log,
            ConcurrentQueue<string> errorLines)
        {
            if (line.StartsWith("INFO:EVENT|", StringComparison.Ordinal))
            {
                ParseEvent(line.Substring("INFO:EVENT|".Length), onEvent);
                return;
            }

            if (line.StartsWith("ERROR:EVENT|", StringComparison.Ordinal))
            {
                string payload = line.Substring("ERROR:EVENT|".Length);
                ParseEvent(payload, onEvent);
                EnqueueError(errorLines, payload);
                return;
            }

            if (line.StartsWith("INFO:", StringComparison.Ordinal))
            {
                log?.Invoke(line.Substring("INFO:".Length).Trim());
                return;
            }

            if (line.StartsWith("WARN:", StringComparison.Ordinal))
            {
                log?.Invoke("警告: " + line.Substring("WARN:".Length).Trim());
                return;
            }

            if (line.StartsWith("ERROR:", StringComparison.Ordinal))
            {
                string message = line.Substring("ERROR:".Length).Trim();
                log?.Invoke("错误: " + message);
                EnqueueError(errorLines, message);
                return;
            }

            log?.Invoke(line.Trim());
        }

        private static void ParseEvent(string payload, Action<BatchConversionEvent>? onEvent)
        {
            if (onEvent == null || string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            string[] parts = payload.Split('|', 3, StringSplitOptions.None);
            if (parts.Length < 2 || !int.TryParse(parts[1], out int index))
            {
                return;
            }

            string message = parts.Length >= 3 ? parts[2] : string.Empty;
            BatchConversionEventKind? kind = parts[0] switch
            {
                "START" => BatchConversionEventKind.Started,
                "DONE" => BatchConversionEventKind.Completed,
                "FAILED" => BatchConversionEventKind.Failed,
                _ => null
            };

            if (kind.HasValue)
            {
                onEvent(new BatchConversionEvent(index, kind.Value, message));
            }
        }

        private static void EnqueueError(ConcurrentQueue<string> errorLines, string line)
        {
            errorLines.Enqueue(line);
            while (errorLines.Count > MaxErrorLines && errorLines.TryDequeue(out _))
            {
            }
        }

        private static string BuildErrorMessage(int exitCode, ConcurrentQueue<string> errorLines)
        {
            if (errorLines.IsEmpty)
            {
                return $"ArcGIS Pro Python 转换失败，退出码: {exitCode}";
            }

            return $"ArcGIS Pro Python 转换失败，退出码: {exitCode}{Environment.NewLine}{string.Join(Environment.NewLine, errorLines)}";
        }
    }
}
