using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.Conversion.TxtToFeature.Infrastructure;

namespace XIAOFUTools.Features.Conversion.TxtToFeature
{
    public partial class TxtToFeatureDockPaneViewModel
    {

        /// <summary>
        /// 处理TXT文件
        /// </summary>
        private async Task ProcessTxtFilesAsync()
        {
            try
            {
                LogMessage("开始搜索TXT文件...");
                var txtFiles = await _fileStore.FindTextFilesAsync(
                    InputFolder,
                    IncludeSubfolders,
                    CancellationToken.None);
                LogMessage($"找到 {txtFiles.Count} 个TXT文件");

                if (txtFiles.Count == 0)
                {
                    LogError("未找到任何TXT文件");
                    return;
                }

                // 只有在不保存到源路径时才创建输出文件夹
                if (!SaveToSourcePath && !string.IsNullOrEmpty(OutputFolder))
                {
                    LogMessage($"创建输出文件夹: {OutputFolder}");
                    await _fileStore.EnsureDirectoryAsync(OutputFolder, CancellationToken.None);
                }

                var allPlots = new List<PlotData>();
                var featureWriter = new ArcGisTxtFeatureWriter(
                    OutputFolder,
                    SeparateFolder,
                    SelectedSpatialReference,
                    FieldNames,
                    LogMessage,
                    LogError,
                    () => CancelRequested);

                for (int i = 0; i < txtFiles.Count; i++)
                {
                    if (CancelRequested)
                    {
                        LogMessage("转换已取消");
                        return;
                    }

                    var txtFile = txtFiles[i];
                    LogMessage($"正在处理文件 {i + 1}/{txtFiles.Count}: {Path.GetFileName(txtFile)}");

                    try
                    {
                        var fileReadResult = await _plotFileReader.ReadAsync(
                            txtFile,
                            FieldNames,
                            SwapXY,
                            CancellationToken.None);
                        LogMessage(fileReadResult.EncodingDescription);
                        LogMessage($"文件大小: {fileReadResult.FileLength} 字节");
                        LogMessage($"成功读取文件，共 {fileReadResult.LineCount} 行");
                        var plots = fileReadResult.Plots;
                        LogMessage($"文件解析完成，共解析到 {plots.Count} 个地块");

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
                                await featureWriter.CreateAsync(plots, cleanFileName, sourceDir);
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
                    await featureWriter.CreateAsync(allPlots, "合并数据", mergeOutputDir);
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
    }
}
