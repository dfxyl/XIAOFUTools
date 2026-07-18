using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.GapCheck
{
    internal partial class GapCheckDockPaneViewModel
    {

        /// <summary>
        /// 执行缝隙检查
        /// </summary>
        private async Task RunGapCheck()
        {
            if (SelectedPolygonLayer == null)
            {
                AddLog("请选择面要素图层");
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                AddLog("请选择输出路径");
                return;
            }

            if (!double.TryParse(Tolerance, out double tolerance) || tolerance < 0)
            {
                AddLog("请输入有效的容差值");
                return;
            }

            try
            {
                IsProcessing = true;
                IsProgressIndeterminate = true;
                StatusMessage = "正在检查缝隙...";
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                AddLog("开始执行缝隙检查...");
                AddLog($"输入图层: {SelectedPolygonLayer.Name}");
                AddLog($"执行容差值: {tolerance} 米");
                AddLog($"输出路径: {OutputPath}");

                await QueuedTask.Run(async () =>
                {
                    await ProcessGapCheck(tolerance, _cancellationTokenSource.Token);
                });

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusMessage = "缝隙检查完成";
                    AddLog("缝隙检查完成！");
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
                AddLog("操作已被用户取消");
            }
            catch (Exception ex)
            {
                StatusMessage = "处理失败";
                AddLog($"处理过程中出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                Progress = 0;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 处理缝隙检查核心逻辑
        /// </summary>
        private async Task ProcessGapCheck(double tolerance, CancellationToken cancellationToken)
        {
            try
            {
                // 使用地理处理工具进行缝隙检查
                await RunGapCheckGeoprocessing(tolerance, cancellationToken);
            }
            catch (Exception ex)
            {
                AddLog($"缝隙检查处理失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 使用地理处理工具执行缝隙检查
        /// </summary>
        private async Task RunGapCheckGeoprocessing(double tolerance, CancellationToken cancellationToken)
        {
            string tempWorkspace = null;
            
            try
            {
                AddLog("正在准备缝隙检查...");
                
                // 验证输入图层
                if (SelectedPolygonLayer == null)
                {
                    AddLog("输入图层无效");
                    return;
                }

                // 安全获取图层名称
                string inputLayerName = null;
                try
                {
                    inputLayerName = SelectedPolygonLayer.Name;
                    if (string.IsNullOrEmpty(inputLayerName))
                    {
                        AddLog("无法获取图层名称");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"获取图层名称失败: {ex.Message}");
                    return;
                }
                
                AddLog($"输入图层: {inputLayerName}");
                
                // 创建临时工作空间路径
                tempWorkspace = _temporaryWorkspaceStore.CreateWorkspace();
                
                string dissolvedPath = Path.Combine(tempWorkspace, "dissolved_features.shp");
                string gapsPath = Path.Combine(tempWorkspace, "gaps.shp");
                string finalGapsPath = Path.Combine(tempWorkspace, "final_gaps.shp");
                
                // 步骤1: 创建融合图层
                AddLog("正在融合所有面要素...");
                
                try
                {
                    var dissolveParams = Geoprocessing.MakeValueArray(
                        inputLayerName,
                        dissolvedPath,
                        "", // 空字符串表示不按字段分组
                        "", // 空字符串表示不统计字段
                        "MULTI_PART", // 允许多部件要素，保留洞
                        "DISSOLVE_LINES" // 融合线
                    );
                    
                    var dissolveResult = await Geoprocessing.ExecuteToolAsync("Dissolve_management", dissolveParams, null, cancellationToken);
                    if (dissolveResult.IsFailed)
                    {
                        AddLog($"融合面要素失败:");
                        foreach (var msg in dissolveResult.Messages)
                        {
                            AddLog($"  - {msg.Text}");
                        }
                        return;
                    }
                    AddLog("面要素融合完成");
                }
                catch (Exception ex)
                {
                    AddLog($"融合面要素时出现异常: {ex.Message}");
                    return;
                }

                // 检查取消请求
                if (cancellationToken.IsCancellationRequested)
                {
                    AddLog("操作已取消");
                    return;
                }

                // 步骤2: 使用要素转面工具提取洞，使用容差参数
                AddLog($"正在提取洞和缝隙（容差: {tolerance}米）...");
                
                try
                {
                    var featureToPolygonParams = Geoprocessing.MakeValueArray(
                        dissolvedPath,
                        gapsPath,
                        $"{tolerance} Meters", // 使用容差参数
                        "ATTRIBUTES", // 保留属性
                        "" // 空字符串表示不使用标签要素
                    );
                    
                    var featureToPolygonResult = await Geoprocessing.ExecuteToolAsync("FeatureToPolygon_management", featureToPolygonParams, null, cancellationToken);
                    if (featureToPolygonResult.IsFailed)
                    {
                        AddLog($"提取洞失败:");
                        foreach (var msg in featureToPolygonResult.Messages)
                        {
                            AddLog($"  - {msg.Text}");
                        }
                        return;
                    }
                    AddLog("洞和缝隙提取完成");
                }
                catch (Exception ex)
                {
                    AddLog($"提取洞时出现异常: {ex.Message}");
                    return;
                }

                // 检查取消请求
                if (cancellationToken.IsCancellationRequested)
                {
                    AddLog("操作已取消");
                    return;
                }

                // 步骤3: 从结果中移除原始面要素，只保留洞
                AddLog("正在过滤出纯洞要素...");
                
                try
                {
                    var eraseParams = Geoprocessing.MakeValueArray(
                        gapsPath,
                        dissolvedPath,
                        finalGapsPath,
                        $"{tolerance} Meters" // 使用容差参数
                    );
                    
                    var eraseResult = await Geoprocessing.ExecuteToolAsync("Erase_analysis", eraseParams, null, cancellationToken);
                    if (eraseResult.IsFailed)
                    {
                        AddLog($"过滤洞要素失败:");
                        foreach (var msg in eraseResult.Messages)
                        {
                            AddLog($"  - {msg.Text}");
                        }
                        return;
                    }
                    AddLog("纯洞要素过滤完成");
                }
                catch (Exception ex)
                {
                    AddLog($"过滤洞要素时出现异常: {ex.Message}");
                    return;
                }

                // 检查取消请求
                if (cancellationToken.IsCancellationRequested)
                {
                    AddLog("操作已取消");
                    return;
                }

                // 步骤4: 添加面积字段
                AddLog("正在计算缝隙面积...");
                
                try
                {
                    // 检查输出文件是否存在要素
                    var checkParams = Geoprocessing.MakeValueArray(finalGapsPath);
                    var checkResult = await Geoprocessing.ExecuteToolAsync("GetCount_management", checkParams);
                    
                    if (checkResult.ReturnValue == "0")
                    {
                        AddLog("未发现缝隙，创建空的输出要素类");
                        
                        // 创建空的输出要素类
                        var createParams = Geoprocessing.MakeValueArray(
                            Path.GetDirectoryName(OutputPath),
                            Path.GetFileNameWithoutExtension(OutputPath),
                            "POLYGON",
                            finalGapsPath // 使用模板
                        );
                        await Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", createParams);
                        
                        AddLog("缝隙检查完成 - 未发现缝隙");
                        return;
                    }
                    
                    var addFieldParams = Geoprocessing.MakeValueArray(
                        finalGapsPath,
                        "GAP_AREA",
                        "DOUBLE",
                        "", // 精度
                        "", // 小数位数
                        "", // 长度
                        "缝隙面积", // 别名
                        "NULLABLE", // 可空
                        "NON_REQUIRED" // 非必需
                    );
                    await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams);
                    
                    var calcFieldParams = Geoprocessing.MakeValueArray(
                        finalGapsPath,
                        "GAP_AREA",
                        "!shape.area!",
                        "PYTHON3"
                    );
                    await Geoprocessing.ExecuteToolAsync("CalculateField_management", calcFieldParams);
                    
                    AddLog("缝隙面积计算完成");
                }
                catch (Exception ex)
                {
                    AddLog($"计算缝隙面积时出现异常: {ex.Message}");
                    // 继续执行，不中断流程
                }

                // 步骤5: 复制最终结果到输出位置
                AddLog("正在复制结果到输出位置...");
                
                try
                {
                    // 使用通用工具检查输出路径是否存在，并提示覆盖
                    var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_缝隙" : "缝隙";
                    var info = OutputDatasetUtils.ParseOutputPath(OutputPath, defName);
                    
                    if (OutputDatasetUtils.Exists(info))
                    {
                        bool overwrite = false;
                        PresentationServices.UiThread.InvokeOrRun(() =>
                        {
                            var msg = info.IsGdb
                                ? $"目标要素类已存在：{info.CatalogPath}。是否覆盖？"
                                : $"目标Shapefile已存在：{info.CatalogPath}。是否覆盖？";
                            var result = PresentationServices.Dialogs.Show(msg, "覆盖确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                            overwrite = result == System.Windows.MessageBoxResult.Yes;
                        });
                        if (!overwrite)
                        {
                            AddLog("用户取消覆盖，操作已中止。");
                            return;
                        }

                        try
                        {
                            await OutputDatasetUtils.DeleteIfExistsAsync(info);
                            AddLog($"删除已存在的数据集: {info.CatalogPath}");
                        }
                        catch (Exception delEx)
                        {
                            AddLog($"删除已有数据集失败: {delEx.Message}");
                        }
                    }
                    
                    var copyParams = Geoprocessing.MakeValueArray(
                        finalGapsPath,
                        OutputPath
                    );
                    
                    var copyResult = await Geoprocessing.ExecuteToolAsync("CopyFeatures_management", copyParams, null, cancellationToken);
                    if (copyResult.IsFailed)
                    {
                        AddLog($"复制缝隙要素失败:");
                        foreach (var msg in copyResult.Messages)
                        {
                            AddLog($"  - {msg.Text}");
                        }
                        return;
                    }
                    AddLog("结果复制完成");
                }
                catch (Exception ex)
                {
                    AddLog($"复制结果时出现异常: {ex.Message}");
                    return;
                }

                // 添加额外字段
                try
                {
                    await AddGapCheckFields(OutputPath, tolerance);
                }
                catch (Exception ex)
                {
                    AddLog($"添加额外字段时出现异常: {ex.Message}");
                    // 不中断流程
                }

                AddLog("缝隙检查完成！");
            }
            catch (OperationCanceledException)
            {
                AddLog("操作已被取消");
                throw;
            }
            catch (Exception ex)
            {
                AddLog($"地理处理执行失败: {ex.Message}");
                AddLog($"异常详情: {ex.ToString()}");
                throw;
            }
            finally
            {
                // 清理资源
                if (!string.IsNullOrEmpty(tempWorkspace))
                {
                    try
                    {
                        // 先移除可能显示的临时图层
                        await RemoveTemporaryLayersFromMap();
                        
                        // 然后清理临时文件
                        await CleanupTemporaryFiles(tempWorkspace);
                    }
                    catch (Exception cleanupEx)
                    {
                        AddLog($"清理资源时出现异常: {cleanupEx.Message}");
                    }
                }
            }
        }
    }
}
