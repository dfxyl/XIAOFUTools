using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.TxtToFeature
{
    /// <summary>
    /// TXT转SHP停靠窗格视图模型
    /// </summary>
    public class TxtToFeatureDockPaneViewModel : INotifyPropertyChanged
    {
        #region 字段

        private string _inputFolder = "";
        private string _outputFolder = "";
        private string _fieldNames = "界址点数,地块面积,地块编号,地块名称,图形类型,图幅号,地块用途,地类编码,描述,@";
        private bool _separateFolder = false;
        private bool _mergeToOneFile = false;
        private bool _swapXY = false;
        private bool _saveToSourcePath = true;
        private bool _includeSubfolders = true;
        private SpatialReference _selectedSpatialReference;
        private string _selectedCoordinateSystemName = "未选择坐标系";
        private bool _isProcessing = false;
        private double _progress = 0;
        private bool _isProgressIndeterminate = false;
        private string _logText = "";
        private string _statusText = "就绪";
        private bool _cancelRequested = false;

        #endregion

        #region 属性

        /// <summary>
        /// 输入文件夹
        /// </summary>
        public string InputFolder
        {
            get => _inputFolder;
            set => SetProperty(ref _inputFolder, value);
        }

        /// <summary>
        /// 输出文件夹
        /// </summary>
        public string OutputFolder
        {
            get => _outputFolder;
            set => SetProperty(ref _outputFolder, value);
        }

        /// <summary>
        /// 字段名称
        /// </summary>
        public string FieldNames
        {
            get => _fieldNames;
            set => SetProperty(ref _fieldNames, value);
        }

        /// <summary>
        /// 单独文件夹
        /// </summary>
        public bool SeparateFolder
        {
            get => _separateFolder;
            set => SetProperty(ref _separateFolder, value);
        }

        /// <summary>
        /// 合并到一个文件
        /// </summary>
        public bool MergeToOneFile
        {
            get => _mergeToOneFile;
            set => SetProperty(ref _mergeToOneFile, value);
        }

        /// <summary>
        /// XY互换
        /// </summary>
        public bool SwapXY
        {
            get => _swapXY;
            set => SetProperty(ref _swapXY, value);
        }

        /// <summary>
        /// 保存在TXT源路径
        /// </summary>
        public bool SaveToSourcePath
        {
            get => _saveToSourcePath;
            set
            {
                SetProperty(ref _saveToSourcePath, value);
                OnPropertyChanged(nameof(ShowOutputFolder));
            }
        }

        /// <summary>
        /// 是否显示输出文件夹组件
        /// </summary>
        public bool ShowOutputFolder => !SaveToSourcePath;

        /// <summary>
        /// 遍历子文件夹
        /// </summary>
        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set => SetProperty(ref _includeSubfolders, value);
        }

        /// <summary>
        /// 选择的坐标系
        /// </summary>
        public SpatialReference SelectedSpatialReference
        {
            get => _selectedSpatialReference;
            set
            {
                SetProperty(ref _selectedSpatialReference, value);
                SelectedCoordinateSystemName = value?.Name ?? "未选择坐标系";
            }
        }

        /// <summary>
        /// 选择的坐标系名称
        /// </summary>
        public string SelectedCoordinateSystemName
        {
            get => _selectedCoordinateSystemName;
            set => SetProperty(ref _selectedCoordinateSystemName, value);
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        /// <summary>
        /// 进度值
        /// </summary>
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        /// <summary>
        /// 进度是否不确定
        /// </summary>
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        /// <summary>
        /// 日志文本
        /// </summary>
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// 是否请求取消
        /// </summary>
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
        }

        #endregion

        #region 命令

        /// <summary>
        /// 选择输入文件夹命令
        /// </summary>
        public ICommand SelectInputFolderCommand { get; }

        /// <summary>
        /// 选择输出文件夹命令
        /// </summary>
        public ICommand SelectOutputFolderCommand { get; }

        /// <summary>
        /// 选择坐标系命令
        /// </summary>
        public ICommand SelectCoordinateSystemCommand { get; }

        /// <summary>
        /// 开始转换命令
        /// </summary>
        public ICommand StartConversionCommand { get; }

        /// <summary>
        /// 停止转换命令
        /// </summary>
        public ICommand StopConversionCommand { get; }

        /// <summary>
        /// 帮助命令
        /// </summary>
        public ICommand HelpCommand { get; }

        #endregion

        #region 构造函数

        public TxtToFeatureDockPaneViewModel()
        {
            // 注册编码提供程序以支持GB2312、GBK等编码
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                LogMessage("编码提供程序注册成功");
            }
            catch (Exception ex)
            {
                LogError($"编码提供程序注册失败: {ex.Message}");
            }

            SelectInputFolderCommand = new RelayCommand(SelectInputFolder);
            SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder);
            SelectCoordinateSystemCommand = new RelayCommand(SelectCoordinateSystem);
            StartConversionCommand = new RelayCommand(async () => await StartConversionAsync(), () => !IsProcessing);
            StopConversionCommand = new RelayCommand(StopConversion, () => IsProcessing);
            HelpCommand = new RelayCommand(ShowHelp);
        }

        #endregion

        #region 方法

        /// <summary>
        /// 选择输入文件夹
        /// </summary>
        private void SelectInputFolder()
        {
            var picked = PathDialogUtils.PickFolder("选择包含TXT文件的输入文件夹", InputFolder);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                InputFolder = picked;
                LogMessage($"已选择输入文件夹: {InputFolder}");
            }
        }

        /// <summary>
        /// 选择输出文件夹
        /// </summary>
        private void SelectOutputFolder()
        {
            var picked = PathDialogUtils.PickFolder("选择输出文件夹", OutputFolder);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                OutputFolder = picked;
                LogMessage($"已选择输出文件夹: {OutputFolder}");
            }
        }

        /// <summary>
        /// 选择坐标系
        /// </summary>
        private void SelectCoordinateSystem()
        {
            try
            {
                var selectedSpatialRef = CoordinateSystemSelector.ShowCoordinateSystemDialog();
                if (selectedSpatialRef != null)
                {
                    SelectedSpatialReference = selectedSpatialRef;
                    LogMessage($"已选择坐标系: {SelectedSpatialReference.Name}");
                }
            }
            catch (Exception ex)
            {
                LogError($"选择坐标系时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始转换
        /// </summary>
        private async Task StartConversionAsync()
        {
            if (!ValidateInputs())
                return;

            IsProcessing = true;
            IsProgressIndeterminate = true;
            Progress = 0;
            LogText = "";
            StatusText = "正在处理...";
            CancelRequested = false;

            try
            {
                LogMessage("开始转换过程...");
                LogMessage($"输入文件夹: {InputFolder}");
                LogMessage($"输出文件夹: {OutputFolder}");
                LogMessage($"坐标系: {SelectedCoordinateSystemName}");
                LogMessage($"字段配置: {FieldNames}");

                // 测试地理处理工具是否可用
                await QueuedTask.Run(() => TestGeoprocessingTools());

                await Task.Run(() => ProcessTxtFiles());

                // 清理可能的临时图层
                await QueuedTask.Run(() => CleanupTemporaryLayers());

                LogMessage("转换完成！");
                StatusText = "转换完成";
            }
            catch (Exception ex)
            {
                LogError($"转换过程中出错: {ex.Message}");
                LogError($"异常类型: {ex.GetType().Name}");
                LogError($"堆栈跟踪: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    LogError($"内部异常: {ex.InnerException.Message}");
                }
                StatusText = "转换失败";
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                Progress = 0;
            }
        }

        /// <summary>
        /// 测试地理处理工具
        /// </summary>
        private void TestGeoprocessingTools()
        {
            try
            {
                LogMessage("测试地理处理工具可用性...");

                // 测试创建临时地理数据库（使用独立临时目录）
                var tempDir = Path.Combine(Path.GetTempPath(), "XIAOFUTools_Temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                var testGdbName = $"test_{Guid.NewGuid():N}.gdb";
                var testGdbPath = Path.Combine(tempDir, testGdbName);

                var createGdbParams = Geoprocessing.MakeValueArray(tempDir, testGdbName);
                var createGdbResult = Geoprocessing.ExecuteToolAsync("CreateFileGDB_management", createGdbParams).Result;

                if (createGdbResult.IsFailed)
                {
                    LogError("地理处理工具测试失败 - 无法创建地理数据库");
                    foreach (var msg in createGdbResult.Messages)
                    {
                        LogError($"  {msg.Type}: {msg.Text}");
                    }
                }
                else
                {
                    LogMessage("地理处理工具测试成功");

                    // 清理测试文件
                    CleanupTempFiles(testGdbPath);
                }
            }
            catch (Exception ex)
            {
                LogError($"测试地理处理工具时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止转换
        /// </summary>
        private void StopConversion()
        {
            CancelRequested = true;
            LogMessage("正在停止转换...");
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            var helpText = @"TXT转SHP工具使用说明：

1. 选择包含TXT文件的输入文件夹
2. 选择输出文件夹
3. 选择目标坐标系
4. 配置字段名称（用逗号分隔）
5. 设置转换选项
6. 点击开始进行转换

支持的文本格式：
- 属性描述部分：包含坐标系、投影等信息
- 地块坐标部分：包含点号、环号、Y坐标、X坐标";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpText, "帮助");
        }

        /// <summary>
        /// 记录消息
        /// </summary>
        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] {message}\n";
            StatusText = message;
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        private void LogError(string error)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] 错误: {error}\n";
            StatusText = $"错误: {error}";
        }

        /// <summary>
        /// 检测文件编码
        /// </summary>
        private Encoding DetectFileEncoding(string filePath)
        {
            try
            {
                // 读取文件的前几个字节来检测BOM
                var bytes = File.ReadAllBytes(filePath);
                if (bytes.Length == 0)
                {
                    LogMessage("文件为空，使用UTF-8编码");
                    return Encoding.UTF8;
                }

                // 检测BOM
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    LogMessage("检测到UTF-8 BOM");
                    return Encoding.UTF8;
                }
                else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                {
                    LogMessage("检测到UTF-16LE BOM");
                    return Encoding.Unicode;
                }
                else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                {
                    LogMessage("检测到UTF-16BE BOM");
                    return Encoding.BigEndianUnicode;
                }

                // 没有BOM，尝试通过内容检测编码
                return DetectEncodingByContent(bytes);
            }
            catch (Exception ex)
            {
                LogError($"检测文件编码时出错: {ex.Message}，使用UTF-8");
                return Encoding.UTF8;
            }
        }

        /// <summary>
        /// 通过内容检测编码
        /// </summary>
        private Encoding DetectEncodingByContent(byte[] bytes)
        {
            try
            {
                // 限制检测的字节数，提高性能
                var sampleSize = Math.Min(bytes.Length, 4096);
                var sampleBytes = new byte[sampleSize];
                Array.Copy(bytes, sampleBytes, sampleSize);

                // 按优先级尝试不同的编码，优先系统默认编码（Windows中文环境下通常是GBK）
                var encodingsToTry = new[]
                {
                    new { Encoding = Encoding.Default, Name = "系统默认(ANSI)", Priority = 1 },
                    new { Encoding = Encoding.GetEncoding("GBK"), Name = "GBK", Priority = 2 },
                    new { Encoding = Encoding.UTF8, Name = "UTF-8", Priority = 3 },
                    new { Encoding = Encoding.GetEncoding("GB2312"), Name = "GB2312", Priority = 4 },
                    new { Encoding = Encoding.GetEncoding("Big5"), Name = "Big5", Priority = 5 }
                };

                var validEncodings = new List<(Encoding encoding, string name, int score)>();

                foreach (var item in encodingsToTry)
                {
                    try
                    {
                        // 使用宽松的验证，允许纯ASCII或包含中文的文件
                        var text = item.Encoding.GetString(sampleBytes);
                        
                        // 检查是否有明显的解码错误
                        bool hasDecodeErrors = text.Contains('\uFFFD');
                        
                        if (!hasDecodeErrors)
                        {
                            var score = CalculateEncodingScore(text, item.Encoding);
                            validEncodings.Add((item.Encoding, item.Name, score));
                            LogMessage($"编码 {item.Name} 验证通过，得分: {score}");
                        }
                        else
                        {
                            LogMessage($"编码 {item.Name} 包含解码错误，跳过");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"编码 {item.Name} 验证失败: {ex.Message}");
                    }
                }

                // 选择得分最高的编码
                if (validEncodings.Any())
                {
                    var bestEncoding = validEncodings.OrderByDescending(e => e.score).First();
                    LogMessage($"选择最佳编码: {bestEncoding.name} (得分: {bestEncoding.score})");
                    return bestEncoding.encoding;
                }

                // 如果所有编码都失败，优先使用系统默认编码（ANSI）
                LogMessage("无法确定文件编码，使用系统默认编码(ANSI)");
                return Encoding.Default;
            }
            catch (Exception ex)
            {
                LogError($"通过内容检测编码时出错: {ex.Message}，使用系统默认编码");
                return Encoding.Default;
            }
        }

        /// <summary>
        /// 计算编码得分
        /// </summary>
        private int CalculateEncodingScore(string text, Encoding encoding)
        {
            int score = 0;

            // 基础分数
            score += 10;

            // 没有替换字符加分
            if (!text.Contains('\uFFFD'))
            {
                score += 20;
            }

            // 系统默认编码优先（在Windows中文环境中通常是GBK/ANSI）
            if (encoding == Encoding.Default)
            {
                score += 25; // 给予最高优先级
            }

            // GBK编码优先（常用于中文文件）
            if (encoding.CodePage == 936 || encoding.CodePage == 54936) // GB2312, GBK
            {
                score += 20;
            }

            // 包含中文字符加分
            bool hasChineseChars = text.Any(c => c >= 0x4E00 && c <= 0x9FFF);
            if (hasChineseChars)
            {
                score += 15;

                // 中文编码处理中文字符额外加分
                if (encoding.CodePage == 936 || encoding.CodePage == 54936 || encoding == Encoding.Default)
                {
                    score += 10;
                }
            }

            // UTF-8编码适当加分（通用性好，但不应高于系统默认编码）
            if (encoding.CodePage == 65001) // UTF-8
            {
                score += 5;
            }

            // 检查是否包含常见的中文标点符号
            var chinesePunctuation = new[] { "，", "。", "；", "：", """, """, "'", "'" };
            if (chinesePunctuation.Any(p => text.Contains(p)))
            {
                score += 8;
            }

            // 纯ASCII文本，系统默认编码和GBK额外加分
            bool isPureAscii = text.All(c => c < 128);
            if (isPureAscii)
            {
                if (encoding == Encoding.Default || encoding.CodePage == 936 || encoding.CodePage == 54936)
                {
                    score += 15; // ASCII在所有编码中都能正确显示，优先使用系统默认编码
                }
            }

            return score;
        }

        /// <summary>
        /// 尝试使用多种编码读取文件内容
        /// </summary>
        private string[] ReadFileWithMultipleEncodings(string filePath)
        {
            var encodingsToTry = new[]
            {
                Encoding.UTF8,
                Encoding.GetEncoding("GBK"),
                Encoding.GetEncoding("GB2312"),
                Encoding.GetEncoding("Big5"),
                Encoding.Default
            };

            Exception lastException = null;

            foreach (var encoding in encodingsToTry)
            {
                try
                {
                    LogMessage($"尝试使用编码 {encoding.EncodingName} 读取文件");
                    var lines = File.ReadAllLines(filePath, encoding);

                    // 检查是否有明显的乱码
                    var sampleText = string.Join("", lines.Take(10));
                    if (!sampleText.Contains('\uFFFD') &&
                        (sampleText.Any(c => c >= 0x4E00 && c <= 0x9FFF) || sampleText.All(c => c < 128)))
                    {
                        LogMessage($"成功使用编码 {encoding.EncodingName} 读取文件");
                        return lines;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    LogMessage($"编码 {encoding.EncodingName} 读取失败: {ex.Message}");
                }
            }

            // 如果所有编码都失败，抛出最后一个异常
            throw new Exception($"无法使用任何编码读取文件: {lastException?.Message}");
        }

        /// <summary>
        /// 检查文本是否包含有效的中文内容
        /// </summary>
        private bool ContainsValidChineseContent(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // 检查是否包含中文字符
            bool hasChineseChars = text.Any(c => c >= 0x4E00 && c <= 0x9FFF);

            // 检查是否包含中文标点
            var chinesePunctuation = new[] { "，", "。", "；", "：", """, """, "'", "'" };
            bool hasChinesePunctuation = chinesePunctuation.Any(p => text.Contains(p));

            // 检查是否包含常见中文词汇
            var commonChineseWords = new[] { "坐标", "地块", "属性", "描述", "面积", "编号" };
            bool hasCommonWords = commonChineseWords.Any(w => text.Contains(w));

            return hasChineseChars || hasChinesePunctuation || hasCommonWords;
        }

        /// <summary>
        /// 清理文件名，移除ArcGIS不支持的字符
        /// </summary>
        private string CleanFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "output";

            // ArcGIS Shapefile文件名不支持的字符
            var invalidChars = new char[] { '-', ' ', '.', '(', ')', '[', ']', '{', '}', '!', '@', '#', '$', '%', '^', '&', '*', '+', '=', '|', '\\', '/', ':', ';', '"', '\'', '<', '>', '?', ',' };

            var cleanName = fileName;

            // 替换无效字符为下划线
            foreach (var invalidChar in invalidChars)
            {
                cleanName = cleanName.Replace(invalidChar, '_');
            }

            // 移除连续的下划线
            while (cleanName.Contains("__"))
            {
                cleanName = cleanName.Replace("__", "_");
            }

            // 移除开头和结尾的下划线
            cleanName = cleanName.Trim('_');

            // 确保不以数字开头
            if (cleanName.Length > 0 && char.IsDigit(cleanName[0]))
            {
                cleanName = "F_" + cleanName;
            }

            // 如果清理后为空，使用默认名称
            if (string.IsNullOrEmpty(cleanName))
            {
                cleanName = "output";
            }

            // 限制长度（Shapefile文件名建议不超过10个字符，但现代系统支持更长）
            if (cleanName.Length > 50)
            {
                cleanName = cleanName.Substring(0, 50).TrimEnd('_');
            }

            LogMessage($"文件名清理: '{fileName}' -> '{cleanName}'");
            return cleanName;
        }

        /// <summary>
        /// 强制清理临时文件
        /// </summary>
        private void CleanupTempFiles(string tempGdbPath)
        {
            try
            {
                if (!Directory.Exists(tempGdbPath))
                {
                    LogMessage("临时文件夹不存在，无需清理");
                    return;
                }

                LogMessage($"开始清理临时文件: {tempGdbPath}");

                // 等待一段时间，确保文件不被占用
                System.Threading.Thread.Sleep(1000);

                // 尝试多次删除
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        // 先尝试设置文件属性为正常
                        SetDirectoryAttributesNormal(tempGdbPath);

                        // 删除目录
                        Directory.Delete(tempGdbPath, true);
                        LogMessage("临时文件清理完成");
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"清理尝试 {i + 1}/5 失败: {ex.Message}");
                        if (i < 4) // 不是最后一次尝试
                        {
                            System.Threading.Thread.Sleep(2000); // 等待2秒后重试
                        }
                    }
                }

                LogMessage("无法完全清理临时文件，将在程序退出时自动清理");
            }
            catch (Exception ex)
            {
                LogMessage($"清理临时文件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置目录及其所有文件的属性为正常
        /// </summary>
        private void SetDirectoryAttributesNormal(string dirPath)
        {
            try
            {
                if (!Directory.Exists(dirPath)) return;

                // 设置目录属性
                var dirInfo = new DirectoryInfo(dirPath);
                dirInfo.Attributes = FileAttributes.Normal;

                // 设置所有文件属性
                foreach (var file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch
                    {
                        // 忽略单个文件的错误
                    }
                }

                // 设置所有子目录属性
                foreach (var subDir in Directory.GetDirectories(dirPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var subDirInfo = new DirectoryInfo(subDir);
                        subDirInfo.Attributes = FileAttributes.Normal;
                    }
                    catch
                    {
                        // 忽略单个目录的错误
                    }
                }
            }
            catch
            {
                // 忽略设置属性的错误
            }
        }

        /// <summary>
        /// 清理地图中的临时图层
        /// </summary>
        private void CleanupTemporaryLayers()
        {
            try
            {
                var map = MapView.Active?.Map;
                if (map == null)
                {
                    LogMessage("当前没有活动地图，无需清理临时图层");
                    return;
                }

                var layersToRemove = new List<Layer>();

                // 查找包含"temp_"的图层
                foreach (var layer in map.GetLayersAsFlattenedList())
                {
                    if (layer.Name.Contains("temp_") ||
                        layer.Name.Contains("临时") ||
                        (layer is FeatureLayer featureLayer &&
                         featureLayer.GetFeatureClass()?.GetDatastore()?.GetPath()?.LocalPath?.Contains("temp_") == true))
                    {
                        layersToRemove.Add(layer);
                        LogMessage($"发现临时图层: {layer.Name}");
                    }
                }

                // 移除临时图层
                if (layersToRemove.Any())
                {
                    map.RemoveLayers(layersToRemove);
                    LogMessage($"已移除 {layersToRemove.Count} 个临时图层");
                }
                else
                {
                    LogMessage("未发现需要清理的临时图层");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"清理临时图层时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据文本格式获取对应的编码（与要素类转TXT工具保持一致）
        /// </summary>
        private Encoding GetEncodingFromFormat(string format)
        {
            try
            {
                switch (format?.ToUpper())
                {
                    case "ANSI":
                        // 在Windows中文环境下，ANSI通常指GBK编码
                        return Encoding.GetEncoding("GBK");
                    case "GBK":
                        return Encoding.GetEncoding("GBK");
                    case "BIG5":
                        return Encoding.GetEncoding("Big5");
                    case "UNICODE":
                        return Encoding.Unicode;
                    case "UTF-8(无BOM)":
                        // 返回无BOM的UTF-8编码
                        return new UTF8Encoding(false);
                    case "UTF-8":
                    default:
                        // 默认UTF-8带BOM
                        return new UTF8Encoding(true);
                }
            }
            catch (Exception ex)
            {
                LogError($"获取编码失败，使用UTF-8: {ex.Message}");
                return new UTF8Encoding(true);
            }
        }

        /// <summary>
        /// 验证编码是否正确
        /// </summary>
        private bool ValidateEncoding(byte[] bytes, Encoding encoding)
        {
            try
            {
                var text = encoding.GetString(bytes);

                // 检查是否有替换字符（乱码标志）
                // 如果没有替换字符，说明编码可以正确解析文件
                return !text.Contains('\uFFFD');
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 验证输入参数
        /// </summary>
        private bool ValidateInputs()
        {
            if (string.IsNullOrEmpty(InputFolder) || !Directory.Exists(InputFolder))
            {
                LogError("请选择有效的输入文件夹");
                return false;
            }

            if (!SaveToSourcePath && string.IsNullOrEmpty(OutputFolder))
            {
                LogError("请选择输出文件夹");
                return false;
            }

            if (SelectedSpatialReference == null)
            {
                LogError("请选择坐标系");
                return false;
            }

            var searchOption = IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var txtFiles = Directory.GetFiles(InputFolder, "*.txt", searchOption);
            if (txtFiles.Length == 0)
            {
                LogError("输入文件夹中没有找到TXT文件");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 处理TXT文件
        /// </summary>
        private void ProcessTxtFiles()
        {
            try
            {
                LogMessage("开始搜索TXT文件...");
                var searchOption = IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var txtFiles = Directory.GetFiles(InputFolder, "*.txt", searchOption);
                LogMessage($"找到 {txtFiles.Length} 个TXT文件");

                if (txtFiles.Length == 0)
                {
                    LogError("未找到任何TXT文件");
                    return;
                }

                // 只有在不保存到源路径时才创建输出文件夹
                if (!SaveToSourcePath && !string.IsNullOrEmpty(OutputFolder) && !Directory.Exists(OutputFolder))
                {
                    LogMessage($"创建输出文件夹: {OutputFolder}");
                    Directory.CreateDirectory(OutputFolder);
                }

                var allPlots = new List<PlotData>();

                for (int i = 0; i < txtFiles.Length; i++)
                {
                    if (CancelRequested)
                    {
                        LogMessage("转换已取消");
                        return;
                    }

                    var txtFile = txtFiles[i];
                    LogMessage($"正在处理文件 {i + 1}/{txtFiles.Length}: {Path.GetFileName(txtFile)}");

                    try
                    {
                        // 检查文件是否存在和可读
                        if (!File.Exists(txtFile))
                        {
                            LogError($"文件不存在: {txtFile}");
                            continue;
                        }

                        var fileInfo = new FileInfo(txtFile);
                        LogMessage($"文件大小: {fileInfo.Length} 字节");

                        var plots = ParseTxtFile(txtFile);
                        LogMessage($"解析到 {plots.Count} 个地块");

                        if (plots.Any())
                        {
                            if (MergeToOneFile)
                            {
                                allPlots.AddRange(plots);
                                LogMessage($"添加 {plots.Count} 个地块到合并列表");
                            }
                            else
                            {
                                var fileName = Path.GetFileNameWithoutExtension(txtFile);
                                // 清理文件名，移除无效字符
                                var cleanFileName = CleanFileName(fileName);
                                LogMessage($"为文件 {fileName} 创建Shapefile (清理后: {cleanFileName})");
                                // 根据SaveToSourcePath选项决定输出路径
                                var sourceDir = SaveToSourcePath ? Path.GetDirectoryName(txtFile) : null;
                                CreateShapefileFromPlots(plots, cleanFileName, sourceDir);
                            }
                        }
                        else
                        {
                            LogMessage($"文件 {Path.GetFileName(txtFile)} 中未找到有效的地块数据");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理文件 {Path.GetFileName(txtFile)} 时出错: {ex.Message}");
                        LogError($"异常详情: {ex.GetType().Name} - {ex.StackTrace}");
                    }
                }

                // 如果选择合并到一个文件，创建合并的Shapefile
                if (MergeToOneFile && allPlots.Any())
                {
                    LogMessage($"创建合并Shapefile，包含 {allPlots.Count} 个地块");
                    // 合并模式下，如果SaveToSourcePath则保存到输入文件夹，否则保存到输出文件夹
                    var mergeOutputDir = SaveToSourcePath ? InputFolder : null;
                    CreateShapefileFromPlots(allPlots, "合并数据", mergeOutputDir);
                }
                else if (MergeToOneFile)
                {
                    LogMessage("合并模式下未找到任何有效地块数据");
                }
            }
            catch (Exception ex)
            {
                LogError($"处理TXT文件时发生严重错误: {ex.Message}");
                LogError($"异常详情: {ex.GetType().Name} - {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 解析TXT文件
        /// </summary>
        private List<PlotData> ParseTxtFile(string filePath)
        {
            var plots = new List<PlotData>();

            try
            {
                // 检测文件编码并读取内容
                var encoding = DetectFileEncoding(filePath);
                var lines = File.ReadAllLines(filePath, encoding);
                LogMessage($"成功读取文件，共 {lines.Length} 行");

                var currentPlot = new PlotData();
                var isInCoordinateSection = false;
                var fieldNames = FieldNames.Split(',').Select(f => f.Trim()).ToArray();

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    // 检查是否是属性描述部分
                    if (line.StartsWith("[属性描述]"))
                    {
                        isInCoordinateSection = false;
                        LogMessage("进入属性描述部分");
                        continue;
                    }

                    // 检查是否是地块坐标部分
                    if (line.StartsWith("[地块坐标]"))
                    {
                        isInCoordinateSection = true;
                        LogMessage("进入地块坐标部分");
                        continue;
                    }

                    if (!isInCoordinateSection)
                    {
                        // 解析属性描述（如坐标系信息等）
                        continue;
                    }

                    // 解析地块数据行
                    if (line.Contains(",") && line.EndsWith("@"))
                    {
                        // 这是属性行，开始新的地块
                        if (currentPlot.Rings.Any())
                        {
                            plots.Add(currentPlot);
                        }

                        currentPlot = new PlotData();
                        ParseAttributeLine(line, currentPlot, fieldNames);
                    }
                    else if (Regex.IsMatch(line, @"^[^,]+,\d+,[\d.]+,[\d.]+"))
                    {
                        // 这是坐标行
                        ParseCoordinateLine(line, currentPlot);
                    }
                }

                // 添加最后一个地块
                if (currentPlot.Rings.Any())
                {
                    plots.Add(currentPlot);
                }

                LogMessage($"文件解析完成，共解析到 {plots.Count} 个地块");
                return plots;
            }
            catch (Exception ex)
            {
                LogError($"解析文件时出错: {ex.Message}");
                return new List<PlotData>();
            }
        }

        /// <summary>
        /// 解析属性行
        /// </summary>
        private void ParseAttributeLine(string line, PlotData plot, string[] fieldNames)
        {
            // 移除末尾的@符号
            line = line.TrimEnd('@', ',');
            var values = line.Split(',');

            for (int i = 0; i < Math.Min(values.Length, fieldNames.Length); i++)
            {
                if (!string.IsNullOrEmpty(fieldNames[i]) && fieldNames[i] != "@")
                {
                    plot.Attributes[fieldNames[i]] = values[i];
                }
            }
        }

        /// <summary>
        /// 解析坐标行
        /// </summary>
        private void ParseCoordinateLine(string line, PlotData plot)
        {
            var parts = line.Split(',');
            if (parts.Length >= 4)
            {
                var pointName = parts[0].Trim();
                if (int.TryParse(parts[1], out int ringNumber) &&
                    double.TryParse(parts[2], out double y) &&
                    double.TryParse(parts[3], out double x))
                {
                    // 根据SwapXY选项决定是否交换坐标
                    if (SwapXY)
                    {
                        (x, y) = (y, x);
                    }

                    var point = new CoordinatePoint
                    {
                        PointName = pointName,
                        RingNumber = ringNumber,
                        X = x,
                        Y = y
                    };

                    // 找到或创建对应的环
                    var ring = plot.Rings.FirstOrDefault(r => r.RingNumber == ringNumber);
                    if (ring == null)
                    {
                        ring = new CoordinateRing { RingNumber = ringNumber };
                        plot.Rings.Add(ring);
                    }

                    ring.Points.Add(point);
                }
            }
        }

        /// <summary>
        /// 从地块数据创建Shapefile
        /// </summary>
        /// <param name="plots">地块数据列表</param>
        /// <param name="fileName">输出文件名</param>
        /// <param name="sourceDirectory">源文件目录（SaveToSourcePath模式下使用），为null时使用OutputFolder</param>
        private void CreateShapefileFromPlots(List<PlotData> plots, string fileName, string sourceDirectory = null)
        {
            if (!plots.Any())
            {
                LogMessage($"跳过创建Shapefile {fileName}：没有地块数据");
                return;
            }

            try
            {
                LogMessage($"正在创建Shapefile: {fileName}，包含 {plots.Count} 个地块");

                var result = QueuedTask.Run(() =>
                {
                    return CreateShapefileInternal(plots, fileName, sourceDirectory);
                }).Result;

                if (result)
                {
                    LogMessage($"Shapefile创建完成: {fileName}");
                }
                else
                {
                    LogError($"Shapefile创建失败: {fileName}");
                }
            }
            catch (Exception ex)
            {
                LogError($"创建Shapefile {fileName} 时出错: {ex.Message}");
                LogError($"异常详情: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    LogError($"内部异常: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// 内部Shapefile创建方法（在QueuedTask中执行）
        /// </summary>
        /// <param name="plots">地块数据列表</param>
        /// <param name="fileName">输出文件名</param>
        /// <param name="sourceDirectory">源文件目录（SaveToSourcePath模式下使用），为null时使用OutputFolder</param>
        private bool CreateShapefileInternal(List<PlotData> plots, string fileName, string sourceDirectory = null)
        {
            try
            {
                LogMessage($"开始内部创建过程: {fileName}");

                // 确定基础输出目录：优先使用sourceDirectory（SaveToSourcePath模式），否则使用OutputFolder
                var baseOutputFolder = sourceDirectory ?? OutputFolder;
                var outputPath = SeparateFolder ?
                    Path.Combine(baseOutputFolder, fileName) :
                    baseOutputFolder;

                if (SeparateFolder && !Directory.Exists(outputPath))
                {
                    LogMessage($"创建输出子文件夹: {outputPath}");
                    Directory.CreateDirectory(outputPath);
                }

                var shapefilePath = Path.Combine(outputPath, $"{fileName}.shp");
                LogMessage($"目标Shapefile路径: {shapefilePath}");

                // 检查文件是否已存在
                if (File.Exists(shapefilePath))
                {
                    LogMessage($"删除已存在的文件: {shapefilePath}");
                    try
                    {
                        File.Delete(shapefilePath);
                        // 删除相关文件
                        var baseName = Path.GetFileNameWithoutExtension(shapefilePath);
                        var directory = Path.GetDirectoryName(shapefilePath);
                        var extensions = new[] { ".shx", ".dbf", ".prj", ".cpg" };
                        foreach (var ext in extensions)
                        {
                            var relatedFile = Path.Combine(directory, baseName + ext);
                            if (File.Exists(relatedFile))
                            {
                                File.Delete(relatedFile);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"删除已存在文件时出错: {ex.Message}");
                    }
                }

                // 使用更简单的方法创建Shapefile
                return CreateShapefileUsingSimpleMethod(plots, shapefilePath);
            }
            catch (Exception ex)
            {
                LogError($"内部创建Shapefile时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 直接创建Shapefile（不使用临时地理数据库）
        /// </summary>
        private bool CreateShapefileUsingSimpleMethod(List<PlotData> plots, string shapefilePath)
        {
            try
            {
                LogMessage("直接创建Shapefile（无临时文件）");

                // 确保输出目录存在
                var outputDir = Path.GetDirectoryName(shapefilePath);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    LogMessage($"创建输出目录: {outputDir}");
                }

                // 使用地理处理工具直接创建Shapefile
                var fileName = Path.GetFileNameWithoutExtension(shapefilePath);
                var createParams = Geoprocessing.MakeValueArray(
                    outputDir,                         // 输出工作空间（文件夹）
                    fileName,                          // 要素类名称（不含扩展名）
                    "POLYGON",                         // 几何类型
                    null,                             // 模板
                    "DISABLED",                       // has_m
                    "DISABLED",                       // has_z
                    SelectedSpatialReference          // 坐标系
                );

                LogMessage($"创建Shapefile: {fileName}");
                var createResult = Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", createParams).Result;

                if (createResult.IsFailed)
                {
                    var errors = string.Join("; ", createResult.Messages.Where(m => m.Type == GPMessageType.Error).Select(m => m.Text));
                    LogError($"创建Shapefile失败: {errors}");
                    return false;
                }

                LogMessage("Shapefile创建成功");

                // 添加字段
                if (!AddFieldsToFeatureClass(shapefilePath))
                {
                    return false;
                }

                // 直接插入要素到Shapefile
                if (!InsertFeaturesToShapefileDirect(shapefilePath, plots))
                {
                    return false;
                }

                LogMessage($"Shapefile创建完成: {shapefilePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"创建Shapefile时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 直接插入要素到Shapefile（使用FileSystemDatastore）
        /// </summary>
        private bool InsertFeaturesToShapefileDirect(string shapefilePath, List<PlotData> plots)
        {
            try
            {
                LogMessage($"开始插入 {plots.Count} 个要素到Shapefile");

                // 使用FileSystemDatastore连接Shapefile
                var shapefileDir = Path.GetDirectoryName(shapefilePath);
                var shapefileName = Path.GetFileNameWithoutExtension(shapefilePath);

                var connectionPath = new FileSystemConnectionPath(new Uri(shapefileDir), FileSystemDatastoreType.Shapefile);

                using (var datastore = new FileSystemDatastore(connectionPath))
                using (var featureClass = datastore.OpenDataset<FeatureClass>(shapefileName))
                {
                    var featureClassDefinition = featureClass.GetDefinition();
                    LogMessage($"Shapefile定义获取成功，字段数: {featureClassDefinition.GetFields().Count}");

                    using (var rowBuffer = featureClass.CreateRowBuffer())
                    using (var insertCursor = featureClass.CreateInsertCursor())
                    {
                        int successCount = 0;
                        int failCount = 0;

                        foreach (var plot in plots)
                        {
                            try
                            {
                                // 创建几何体
                                var polygon = CreatePolygonFromPlot(plot);
                                if (polygon != null)
                                {
                                    rowBuffer[featureClassDefinition.GetShapeField()] = polygon;

                                    // 设置属性值
                                    foreach (var attr in plot.Attributes)
                                    {
                                        var fieldIndex = featureClassDefinition.FindField(attr.Key);
                                        if (fieldIndex >= 0)
                                        {
                                            rowBuffer[fieldIndex] = attr.Value?.ToString() ?? "";
                                        }
                                    }

                                    insertCursor.Insert(rowBuffer);
                                    successCount++;
                                }
                                else
                                {
                                    LogError("创建多边形几何体失败");
                                    failCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError($"插入单个要素时出错: {ex.Message}");
                                failCount++;
                            }
                        }

                        LogMessage($"要素插入完成: 成功 {successCount} 个，失败 {failCount} 个");
                        return successCount > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"插入要素到Shapefile时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 为要素类添加字段
        /// </summary>
        private bool AddFieldsToFeatureClass(string featureClassPath)
        {
            try
            {
                var fieldNames = FieldNames.Split(',').Select(f => f.Trim()).Where(f => !string.IsNullOrEmpty(f) && f != "@").ToArray();
                LogMessage($"准备添加 {fieldNames.Length} 个字段");

                foreach (var fieldName in fieldNames)
                {
                    LogMessage($"添加字段: {fieldName}");

                    var parameters = Geoprocessing.MakeValueArray(
                        featureClassPath,
                        fieldName,
                        "TEXT",
                        null, // precision
                        null, // scale
                        255   // length
                    );

                    var result = Geoprocessing.ExecuteToolAsync("AddField_management", parameters).Result;

                    if (result.IsFailed)
                    {
                        var errors = string.Join("; ", result.Messages.Where(m => m.Type == GPMessageType.Error).Select(m => m.Text));
                        LogError($"添加字段 {fieldName} 失败: {errors}");
                        return false;
                    }
                    else
                    {
                        LogMessage($"字段 {fieldName} 添加成功");
                    }
                }

                LogMessage("所有字段添加完成");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"添加字段时出错: {ex.Message}");
                return false;
            }
        }







        /// <summary>
        /// 从地块数据创建多边形几何体
        /// </summary>
        private Polygon CreatePolygonFromPlot(PlotData plot)
        {
            if (!plot.Rings.Any())
            {
                LogError("地块数据中没有环信息");
                return null;
            }

            try
            {
                LogMessage($"创建多边形，包含 {plot.Rings.Count} 个环");

                var polygonBuilder = new PolygonBuilderEx(SelectedSpatialReference);

                // 按环号排序，外环通常是1，内环是2、3、4等
                var sortedRings = plot.Rings.OrderBy(r => r.RingNumber).ToList();
                LogMessage($"环排序完成，环号: {string.Join(", ", sortedRings.Select(r => r.RingNumber))}");

                foreach (var ring in sortedRings)
                {
                    LogMessage($"处理环 {ring.RingNumber}，包含 {ring.Points.Count} 个点");

                    if (ring.Points.Count < 3)
                    {
                        LogError($"环 {ring.RingNumber} 点数不足（{ring.Points.Count} < 3），跳过");
                        continue;
                    }

                    var coordinates = new List<Coordinate2D>();

                    // 添加所有坐标点
                    foreach (var point in ring.Points)
                    {
                        coordinates.Add(new Coordinate2D(point.X, point.Y));
                    }

                    LogMessage($"环 {ring.RingNumber} 坐标点添加完成: {coordinates.Count} 个点");

                    // 检查是否需要闭合
                    var firstPoint = coordinates.First();
                    var lastPoint = coordinates.Last();
                    if (Math.Abs(firstPoint.X - lastPoint.X) > 0.001 || Math.Abs(firstPoint.Y - lastPoint.Y) > 0.001)
                    {
                        coordinates.Add(firstPoint); // 添加闭合点
                        LogMessage($"环 {ring.RingNumber} 添加闭合点");
                    }

                    // 第一个环作为外环，其他环作为内环
                    if (ring.RingNumber == 1 || ring == sortedRings.First())
                    {
                        LogMessage($"添加外环 {ring.RingNumber}");
                        polygonBuilder.AddPart(coordinates);
                    }
                    else
                    {
                        LogMessage($"添加内环 {ring.RingNumber}（逆时针）");
                        // 内环需要逆时针方向
                        coordinates.Reverse();
                        polygonBuilder.AddPart(coordinates);
                    }
                }

                var polygon = polygonBuilder.ToGeometry();
                LogMessage($"多边形创建成功，面积: {polygon.Area:F2}");
                return polygon;
            }
            catch (Exception ex)
            {
                LogError($"创建多边形几何体时出错: {ex.Message}");
                LogError($"异常详情: {ex.GetType().Name} - {ex.StackTrace}");
                return null;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    /// <summary>
    /// 简单的RelayCommand实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();
    }

    /// <summary>
    /// 地块数据
    /// </summary>
    public class PlotData
    {
        public Dictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
        public List<CoordinateRing> Rings { get; set; } = new List<CoordinateRing>();
    }

    /// <summary>
    /// 坐标环
    /// </summary>
    public class CoordinateRing
    {
        public int RingNumber { get; set; }
        public List<CoordinatePoint> Points { get; set; } = new List<CoordinatePoint>();
    }

    /// <summary>
    /// 坐标点
    /// </summary>
    public class CoordinatePoint
    {
        public string PointName { get; set; }
        public int RingNumber { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
