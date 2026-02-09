using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace XIAOFUTools.Tools.User.AIAssistant.Services
{
    /// <summary>
    /// Python代码执行结果
    /// </summary>
    public class PythonExecutionResult
    {
        public bool Success { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public int ExitCode { get; set; }
        public long ExecutionTimeMs { get; set; }
    }

    /// <summary>
    /// Python执行服务 - 使用ArcGIS Pro Geoprocessing API执行Python代码
    /// </summary>
    public class PythonExecutionService
    {
        private static PythonExecutionService _instance;
        private static readonly object _lock = new object();
        private static readonly List<(System.Text.RegularExpressions.Regex Pattern, string Reason)> DangerousPatterns =
            new List<(System.Text.RegularExpressions.Regex, string)>
            {
                (new System.Text.RegularExpressions.Regex(@"\bos\.remove\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "删除文件"),
                (new System.Text.RegularExpressions.Regex(@"\bos\.rmdir\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "删除目录"),
                (new System.Text.RegularExpressions.Regex(@"\bshutil\.rmtree\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "递归删除目录"),
                (new System.Text.RegularExpressions.Regex(@"\barcpy\.Delete(_management)?\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "删除地理数据"),
                (new System.Text.RegularExpressions.Regex(@"\barcpy\.management\.Delete\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "删除地理数据"),
                (new System.Text.RegularExpressions.Regex(@"\barcpy\.management\.DeleteFeatures\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "删除要素"),
                (new System.Text.RegularExpressions.Regex(@"\barcpy\.DeleteRows_management\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "删除表行"),
                (new System.Text.RegularExpressions.Regex(@"\barcpy\.management\.DeleteRows\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "删除表行"),
                (new System.Text.RegularExpressions.Regex(@"\barcpy\.Truncate(Table)?(?:_management)?\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "截断表"),
                (new System.Text.RegularExpressions.Regex(@"\barcpy\.management\.TruncateTable\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "截断表"),
                (new System.Text.RegularExpressions.Regex(@"\bsubprocess\.(run|call|Popen)\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "执行外部进程"),
                (new System.Text.RegularExpressions.Regex(@"\bos\.system\s*\(", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "执行系统命令")
            };
        
        // 固定工具箱目录（用于"不询问"模式）
        private readonly string _fixedToolboxDir;
        private readonly string _fixedToolboxPath;
        private bool _fixedToolboxInitialized = false;
        
        /// <summary>
        /// 是否使用固定位置（不弹窗模式）
        /// </summary>
        public bool UseFixedLocation { get; set; } = false;

        /// <summary>
        /// 单例实例
        /// </summary>
        public static PythonExecutionService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new PythonExecutionService();
                        }
                    }
                }
                return _instance;
            }
        }

        private PythonExecutionService() 
        {
            // 固定工具箱位置（使用稳定的用户数据目录，避免AssemblyCache的GUID变化问题）
            _fixedToolboxDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XIAOFUTools", "PythonExecutor");
            _fixedToolboxPath = Path.Combine(_fixedToolboxDir, "ExecutorToolbox.pyt");
            
            // 从设置加载模式
            LoadSettings();
            
            // 尝试将工具箱目录添加到受信任位置（避免每次弹窗）
            TryAddToTrustedLocations();
        }
        
        /// <summary>
        /// 尝试将固定工具箱添加到ArcGIS Pro的.access_pyt信任列表
        /// 这样重启Pro后不会再弹出安全确认框
        /// </summary>
        private void TryAddToTrustedLocations()
        {
            try
            {
                // 确保目录存在
                if (!Directory.Exists(_fixedToolboxDir))
                    Directory.CreateDirectory(_fixedToolboxDir);
                
                // ArcGIS Pro 的 Python Toolbox 信任列表文件
                var accessPytFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ESRI", "ArcGISPro", "Geoprocessing", ".access_pyt");
                
                // 确保目录存在
                var gpDir = Path.GetDirectoryName(accessPytFile);
                if (!Directory.Exists(gpDir))
                    Directory.CreateDirectory(gpDir);
                
                // 生成当前时间戳（Windows FILETIME格式）
                var timestamp = DateTime.UtcNow.ToFileTime();
                var trustedEntry = $"1|{timestamp}|{_fixedToolboxPath}";
                
                if (File.Exists(accessPytFile))
                {
                    var content = File.ReadAllText(accessPytFile);
                    
                    // 检查是否已包含我们的工具箱路径
                    if (content.Contains(_fixedToolboxPath))
                    {
                        // 检查是否已经是信任状态（以1|开头）
                        // 如果是0|开头，需要更新为1|
                        var pattern = $"\"0|";
                        var pathPattern = _fixedToolboxPath.Replace("\\", "\\\\");
                        
                        if (content.Contains($"\"0|") && content.Contains(_fixedToolboxPath))
                        {
                            // 需要将0改为1（从拒绝改为信任）
                            // 使用正则替换
                            var regex = new System.Text.RegularExpressions.Regex(
                                $"\"0\\|\\d+\\|{System.Text.RegularExpressions.Regex.Escape(_fixedToolboxPath)}\"");
                            content = regex.Replace(content, $"\"{trustedEntry}\"");
                            File.WriteAllText(accessPytFile, content);
                            System.Diagnostics.Debug.WriteLine($"[PythonExecutionService] 已更新工具箱信任状态: {_fixedToolboxPath}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[PythonExecutionService] 工具箱已在信任列表中");
                        }
                        return;
                    }
                    
                    // 添加新条目到JSON数组
                    if (content.StartsWith("[") && content.EndsWith("]"))
                    {
                        // 在数组末尾添加
                        var newContent = content.TrimEnd(']') + $",\"{trustedEntry}\"]";
                        File.WriteAllText(accessPytFile, newContent);
                        System.Diagnostics.Debug.WriteLine($"[PythonExecutionService] 已添加工具箱到信任列表: {_fixedToolboxPath}");
                    }
                }
                else
                {
                    // 创建新的信任列表文件
                    var newContent = $"[\"{trustedEntry}\"]";
                    File.WriteAllText(accessPytFile, newContent);
                    System.Diagnostics.Debug.WriteLine($"[PythonExecutionService] 已创建信任列表文件: {_fixedToolboxPath}");
                }
            }
            catch (Exception ex)
            {
                // 添加失败不影响功能，只是第一次会弹窗
                System.Diagnostics.Debug.WriteLine($"[PythonExecutionService] 添加信任列表失败（不影响功能）: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 加载设置
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                var settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XIAOFUTools", "python_settings.txt");
                if (File.Exists(settingsPath))
                {
                    var mode = File.ReadAllText(settingsPath).Trim();
                    UseFixedLocation = mode == "fixed";
                }
            }
            catch { }
        }
        
        /// <summary>
        /// 保存设置
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XIAOFUTools");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                
                var settingsPath = Path.Combine(dir, "python_settings.txt");
                File.WriteAllText(settingsPath, UseFixedLocation ? "fixed" : "ask");
            }
            catch { }
        }
        
        /// <summary>
        /// 获取固定工具箱路径（用于显示给用户）
        /// </summary>
        public string FixedToolboxPath => _fixedToolboxPath;

        /// <summary>
        /// 检查Python环境是否可用（使用GP API时始终可用）
        /// </summary>
        public bool IsPythonAvailable => true;

        /// <summary>
        /// 执行Python代码（在ArcGIS Pro当前会话中执行，可访问当前地图）
        /// 使用动态Python Toolbox通过Geoprocessing API执行
        /// </summary>
        /// <param name="code">Python代码</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeoutMs">超时时间（毫秒），默认30秒</param>
        /// <returns>执行结果</returns>
        public async Task<PythonExecutionResult> ExecuteCodeAsync(
            string code, 
            CancellationToken cancellationToken = default,
            int timeoutMs = 30000)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return new PythonExecutionResult
                {
                    Success = false,
                    Error = "代码不能为空",
                    ExitCode = -1,
                    ExecutionTimeMs = 0
                };
            }

            if (TryGetSafetyBlockReason(code, out var reason))
            {
                return new PythonExecutionResult
                {
                    Success = false,
                    Error = $"检测到高风险操作（{reason}），已拦截执行。请改为只读分析代码后再试。",
                    ExitCode = -2,
                    ExecutionTimeMs = 0
                };
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeoutMs > 0)
            {
                timeoutCts.CancelAfter(timeoutMs);
            }

            var effectiveToken = timeoutCts.Token;

            // 根据设置选择执行模式
            if (UseFixedLocation)
            {
                return await ExecuteWithFixedToolbox(code, effectiveToken, timeoutMs);
            }
            else
            {
                return await ExecuteWithTempToolbox(code, effectiveToken, timeoutMs);
            }
        }
        
        /// <summary>
        /// 使用临时工具箱执行（每次询问模式）
        /// </summary>
        private async Task<PythonExecutionResult> ExecuteWithTempToolbox(
            string code, 
            CancellationToken cancellationToken,
            int timeoutMs)
        {
            var stopwatch = Stopwatch.StartNew();
            string tempDir = null;
            string outputFile = null;
            string errorFile = null;

            try
            {
                // 创建临时目录
                tempDir = Path.Combine(Path.GetTempPath(), $"ArcGISPythonExec_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);
                
                outputFile = Path.Combine(tempDir, "output.txt");
                errorFile = Path.Combine(tempDir, "error.txt");
                var toolboxPath = Path.Combine(tempDir, "DynamicToolbox.pyt");
                var codePath = Path.Combine(tempDir, "user_code.py");
                
                // 使用不带BOM的UTF-8编码
                var utf8NoBom = new UTF8Encoding(false);
                
                // 保存用户代码到文件
                await File.WriteAllTextAsync(codePath, code, utf8NoBom, cancellationToken);
                
                // 创建动态Python Toolbox
                var toolboxContent = GeneratePythonToolbox(codePath, outputFile, errorFile);
                await File.WriteAllTextAsync(toolboxPath, toolboxContent, utf8NoBom, cancellationToken);

                System.Diagnostics.Debug.WriteLine($"[PythonExecutionService] 创建临时Toolbox: {toolboxPath}");

                // 使用QueuedTask在ArcGIS Pro主线程执行GP工具
                var gpResult = await QueuedTask.Run(async () =>
                {
                    try
                    {
                        // 执行Python Toolbox中的工具
                        var toolPath = $"{toolboxPath}\\ExecuteCode";
                        var parameters = Geoprocessing.MakeValueArray();
                        
                        var result = await Geoprocessing.ExecuteToolAsync(
                            toolPath, 
                            parameters,
                            null,
                            cancellationToken,
                            (eventName, o) =>
                            {
                                System.Diagnostics.Debug.WriteLine($"[GP Event] {eventName}: {o}");
                            },
                            GPExecuteToolFlags.GPThread);
                        
                        return result;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PythonExecutionService] GP执行异常: {ex.Message}");
                        throw;
                    }
                });

                stopwatch.Stop();

                // 读取输出和错误
                var output = File.Exists(outputFile) ? await File.ReadAllTextAsync(outputFile, Encoding.UTF8) : "";
                var error = File.Exists(errorFile) ? await File.ReadAllTextAsync(errorFile, Encoding.UTF8) : "";

                // 检查GP结果
                bool success = !gpResult.IsFailed;
                
                // 如果GP成功但有错误输出，仍然标记为失败
                if (success && !string.IsNullOrWhiteSpace(error))
                {
                    success = false;
                }

                // 收集GP消息
                var gpMessages = new StringBuilder();
                if (gpResult.Messages != null)
                {
                    foreach (var msg in gpResult.Messages)
                    {
                        if (msg.Type == GPMessageType.Error || msg.Type == GPMessageType.Warning)
                        {
                            gpMessages.AppendLine($"[{msg.Type}] {msg.Text}");
                        }
                    }
                }
                
                if (gpMessages.Length > 0 && string.IsNullOrWhiteSpace(error))
                {
                    error = gpMessages.ToString();
                }

                System.Diagnostics.Debug.WriteLine($"[PythonExecutionService] GP执行完成, Success={success}, 耗时={stopwatch.ElapsedMilliseconds}ms");

                return new PythonExecutionResult
                {
                    Success = success,
                    Output = output.Trim(),
                    Error = error.Trim(),
                    ExitCode = success ? 0 : 1,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return new PythonExecutionResult
                {
                    Success = false,
                    Error = "执行已被用户取消",
                    ExitCode = -1,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"[PythonExecutionService] 执行异常: {ex.Message}");
                
                return new PythonExecutionResult
                {
                    Success = false,
                    Error = $"执行出错: {ex.Message}",
                    ExitCode = -1,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                // 清理临时目录
                if (tempDir != null && Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        // 忽略清理错误
                    }
                }
            }
        }
        
        /// <summary>
        /// 使用固定工具箱执行（不询问模式，信任一次后不再弹窗）
        /// </summary>
        private async Task<PythonExecutionResult> ExecuteWithFixedToolbox(
            string code, 
            CancellationToken cancellationToken,
            int timeoutMs)
        {
            var stopwatch = Stopwatch.StartNew();
            string codePath = null;
            string outputFile = null;
            string errorFile = null;

            try
            {
                // 确保固定工具箱存在
                EnsureFixedToolboxExists();
                
                // 生成唯一执行ID
                var execId = Guid.NewGuid().ToString("N");
                codePath = Path.Combine(_fixedToolboxDir, $"code_{execId}.py");
                outputFile = Path.Combine(_fixedToolboxDir, $"output_{execId}.txt");
                errorFile = Path.Combine(_fixedToolboxDir, $"error_{execId}.txt");
                
                var utf8NoBom = new UTF8Encoding(false);
                
                // 保存用户代码
                await File.WriteAllTextAsync(codePath, code, utf8NoBom, cancellationToken);
                
                // 创建参数文件
                var paramsFile = Path.Combine(_fixedToolboxDir, "current_params.txt");
                await File.WriteAllTextAsync(paramsFile, $"{codePath}|{outputFile}|{errorFile}", utf8NoBom, cancellationToken);

                System.Diagnostics.Debug.WriteLine($"[PythonExecutionService] 使用固定Toolbox: {_fixedToolboxPath}");

                var gpResult = await QueuedTask.Run(async () =>
                {
                    var toolPath = $"{_fixedToolboxPath}\\ExecuteCode";
                    var parameters = Geoprocessing.MakeValueArray();
                    
                    return await Geoprocessing.ExecuteToolAsync(
                        toolPath, parameters, null, cancellationToken,
                        (eventName, o) => System.Diagnostics.Debug.WriteLine($"[GP Event] {eventName}: {o}"),
                        GPExecuteToolFlags.GPThread);
                });

                stopwatch.Stop();

                var output = File.Exists(outputFile) ? await File.ReadAllTextAsync(outputFile, Encoding.UTF8) : "";
                var error = File.Exists(errorFile) ? await File.ReadAllTextAsync(errorFile, Encoding.UTF8) : "";

                bool success = !gpResult.IsFailed;
                if (success && !string.IsNullOrWhiteSpace(error)) success = false;

                var gpMessages = new StringBuilder();
                if (gpResult.Messages != null)
                {
                    foreach (var msg in gpResult.Messages)
                    {
                        if (msg.Type == GPMessageType.Error || msg.Type == GPMessageType.Warning)
                            gpMessages.AppendLine($"[{msg.Type}] {msg.Text}");
                    }
                }
                if (gpMessages.Length > 0 && string.IsNullOrWhiteSpace(error))
                    error = gpMessages.ToString();

                return new PythonExecutionResult
                {
                    Success = success,
                    Output = output.Trim(),
                    Error = error.Trim(),
                    ExitCode = success ? 0 : 1,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return new PythonExecutionResult { Success = false, Error = "执行已被用户取消", ExitCode = -1, ExecutionTimeMs = stopwatch.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new PythonExecutionResult { Success = false, Error = $"执行出错: {ex.Message}", ExitCode = -1, ExecutionTimeMs = stopwatch.ElapsedMilliseconds };
            }
            finally
            {
                // 清理临时文件（保留工具箱）
                try
                {
                    if (codePath != null && File.Exists(codePath)) File.Delete(codePath);
                    if (outputFile != null && File.Exists(outputFile)) File.Delete(outputFile);
                    if (errorFile != null && File.Exists(errorFile)) File.Delete(errorFile);
                }
                catch { }
            }
        }
        
        /// <summary>
        /// 确保固定工具箱存在
        /// </summary>
        private void EnsureFixedToolboxExists()
        {
            if (_fixedToolboxInitialized && File.Exists(_fixedToolboxPath))
                return;
                
            if (!Directory.Exists(_fixedToolboxDir))
                Directory.CreateDirectory(_fixedToolboxDir);
            
            var utf8NoBom = new UTF8Encoding(false);
            var content = GenerateFixedToolbox();
            File.WriteAllText(_fixedToolboxPath, content, utf8NoBom);
            
            _fixedToolboxInitialized = true;
            System.Diagnostics.Debug.WriteLine($"[PythonExecutionService] 初始化固定工具箱: {_fixedToolboxPath}");
        }
        
        /// <summary>
        /// 生成固定工具箱（从参数文件读取路径）
        /// </summary>
        private string GenerateFixedToolbox()
        {
            var paramsFileEscaped = Path.Combine(_fixedToolboxDir, "current_params.txt").Replace("\\", "\\\\");
            return $@"# -*- coding: utf-8 -*-
import arcpy, sys, io, traceback

class Toolbox(object):
    def __init__(self):
        self.label = ""XIAOFUTools Python Executor""
        self.alias = ""xiaofupy""
        self.tools = [ExecuteCode]

class ExecuteCode(object):
    def __init__(self):
        self.label = ""Execute Code""
        self.canRunInBackground = False

    def getParameterInfo(self):
        return []

    def execute(self, parameters, messages):
        with open(r""{paramsFileEscaped}"", 'r', encoding='utf-8') as f:
            params = f.read().strip().split('|')
        code_file, output_file, error_file = params[0], params[1], params[2]
        
        old_stdout, old_stderr = sys.stdout, sys.stderr
        stdout_capture, stderr_capture = io.StringIO(), io.StringIO()
        sys.stdout, sys.stderr = stdout_capture, stderr_capture
        
        try:
            with open(code_file, 'r', encoding='utf-8') as f:
                exec(f.read(), {{'__name__': '__main__', '__builtins__': __builtins__, 'arcpy': arcpy}})
        except:
            stderr_capture.write(traceback.format_exc())
        finally:
            sys.stdout, sys.stderr = old_stdout, old_stderr
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write(stdout_capture.getvalue())
            with open(error_file, 'w', encoding='utf-8') as f:
                f.write(stderr_capture.getvalue())
";
        }

        /// <summary>
        /// 生成动态Python Toolbox内容
        /// </summary>
        private string GeneratePythonToolbox(string codePath, string outputFile, string errorFile)
        {
            var codePathEscaped = codePath.Replace("\\", "\\\\");
            var outputFileEscaped = outputFile.Replace("\\", "\\\\");
            var errorFileEscaped = errorFile.Replace("\\", "\\\\");

            return $@"# -*- coding: utf-8 -*-
import arcpy
import sys
import io
import traceback

class Toolbox(object):
    def __init__(self):
        self.label = ""Dynamic Python Executor""
        self.alias = ""dynpyexec""
        self.tools = [ExecuteCode]

class ExecuteCode(object):
    def __init__(self):
        self.label = ""Execute Code""
        self.description = ""Execute Python code in current ArcGIS Pro session""
        self.canRunInBackground = False

    def getParameterInfo(self):
        return []

    def execute(self, parameters, messages):
        output_file = r""{outputFileEscaped}""
        error_file = r""{errorFileEscaped}""
        code_file = r""{codePathEscaped}""
        
        old_stdout = sys.stdout
        old_stderr = sys.stderr
        
        stdout_capture = io.StringIO()
        stderr_capture = io.StringIO()
        
        sys.stdout = stdout_capture
        sys.stderr = stderr_capture
        
        try:
            with open(code_file, 'r', encoding='utf-8') as f:
                user_code = f.read()
            
            exec_globals = {{
                '__name__': '__main__',
                '__builtins__': __builtins__,
                'arcpy': arcpy
            }}
            exec(user_code, exec_globals)
            
        except Exception as e:
            stderr_capture.write(traceback.format_exc())
        finally:
            sys.stdout = old_stdout
            sys.stderr = old_stderr
            
            with open(output_file, 'w', encoding='utf-8') as f:
                f.write(stdout_capture.getvalue())
            
            with open(error_file, 'w', encoding='utf-8') as f:
                f.write(stderr_capture.getvalue())
        
        return
";
        }

        /// <summary>
        /// 在QueuedTask中执行Python代码（确保在正确的线程上下文）
        /// </summary>
        public async Task<PythonExecutionResult> ExecuteCodeInQueuedTaskAsync(
            string code,
            CancellationToken cancellationToken = default,
            int timeoutMs = 30000)
        {
            return await QueuedTask.Run(async () =>
            {
                return await ExecuteCodeAsync(code, cancellationToken, timeoutMs);
            });
        }

        /// <summary>
        /// 测试Python环境
        /// </summary>
        public async Task<PythonExecutionResult> TestEnvironmentAsync()
        {
            var testCode = @"
import sys
print(f'Python版本: {sys.version}')
print(f'Python路径: {sys.executable}')

try:
    import arcpy
    print(f'ArcPy版本: {arcpy.GetInstallInfo()[""Version""]}')
    print('ArcPy环境正常')
except ImportError as e:
    print(f'ArcPy导入失败: {e}')
except Exception as e:
    print(f'ArcPy错误: {e}')
";
            return await ExecuteCodeAsync(testCode);
        }

        private static bool TryGetSafetyBlockReason(string code, out string reason)
        {
            reason = null;

            foreach (var rule in DangerousPatterns)
            {
                if (rule.Pattern.IsMatch(code))
                {
                    reason = rule.Reason;
                    return true;
                }
            }

            return false;
        }
    }
}
