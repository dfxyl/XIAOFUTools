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
        private void InitializeCommands()
        {
            try
            {
                BrowseOutputCommand = new RelayCommand(BrowseOutput);
                RunCommand = new RelayCommand(async () => await RunGapCheck(), () => CanProcess);
                CancelCommand = new RelayCommand(CancelProcess);
                ShowHelpCommand = new RelayCommand(ShowHelp);
                RefreshLayersCommand = new RelayCommand(RefreshLayers);
                
                AddLog("命令初始化完成");
            }
            catch (Exception ex)
            {
                AddLog($"命令初始化失败: {ex.Message}");
            }
        }

        private void InitializeData()
        {
            // 确保所有字符串属性都有初始值
            if (string.IsNullOrEmpty(_logContent))
                _logContent = "";
            if (string.IsNullOrEmpty(_outputPath))
                _outputPath = "";
            if (string.IsNullOrEmpty(_statusMessage))
                _statusMessage = "准备就绪";
                
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            AddLog("缝隙检查工具已启动");
        }

        /// <summary>
        /// 初始化界面
        /// </summary>
        public void Initialize()
        {
            LoadPolygonLayers();
        }



        /// <summary>
        /// 为输出要素类添加缝隙检查相关字段
        /// </summary>
        private async Task AddGapCheckFields(string outputPath, double tolerance)
        {
            try
            {
                AddLog("正在添加缝隙检查相关字段...");
                
                // 检查输出路径是否有效
                if (string.IsNullOrEmpty(outputPath))
                {
                    AddLog("输出路径无效，跳过字段添加");
                    return;
                }

                // 添加缝隙ID字段
                try
                {
                    var addFieldParams1 = Geoprocessing.MakeValueArray(
                        outputPath,
                        "GAP_ID",
                        "LONG",
                        "", // 精度
                        "", // 小数位数
                        "", // 长度
                        "缝隙编号", // 别名
                        "NULLABLE", // 可空
                        "NON_REQUIRED" // 非必需
                    );
                    var result1 = await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams1);
                    if (result1.IsFailed)
                    {
                        AddLog("添加GAP_ID字段失败，可能字段已存在");
                    }
                    else
                    {
                        AddLog("GAP_ID字段添加成功");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"添加GAP_ID字段时出现异常: {ex.Message}");
                }

                // 添加容差字段
                try
                {
                    var addFieldParams2 = Geoprocessing.MakeValueArray(
                        outputPath,
                        "TOLERANCE",
                        "DOUBLE",
                        "", // 精度
                        "", // 小数位数
                        "", // 长度
                        "检查容差", // 别名
                        "NULLABLE", // 可空
                        "NON_REQUIRED" // 非必需
                    );
                    var result2 = await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams2);
                    if (result2.IsFailed)
                    {
                        AddLog("添加TOLERANCE字段失败，可能字段已存在");
                    }
                    else
                    {
                        AddLog("TOLERANCE字段添加成功");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"添加TOLERANCE字段时出现异常: {ex.Message}");
                }

                // 计算缝隙ID
                try
                {
                    var calcFieldParams1 = Geoprocessing.MakeValueArray(
                        outputPath,
                        "GAP_ID",
                        "!OBJECTID!",
                        "PYTHON3"
                    );
                    var result3 = await Geoprocessing.ExecuteToolAsync("CalculateField_management", calcFieldParams1);
                    if (result3.IsFailed)
                    {
                        AddLog("计算GAP_ID字段失败");
                    }
                    else
                    {
                        AddLog("GAP_ID字段计算完成");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"计算GAP_ID字段时出现异常: {ex.Message}");
                }

                // 设置容差值（执行精度）
                try
                {
                    var calcFieldParams2 = Geoprocessing.MakeValueArray(
                        outputPath,
                        "TOLERANCE",
                        tolerance.ToString(),
                        "PYTHON3"
                    );
                    var result4 = await Geoprocessing.ExecuteToolAsync("CalculateField_management", calcFieldParams2);
                    if (result4.IsFailed)
                    {
                        AddLog("设置TOLERANCE字段值失败");
                    }
                    else
                    {
                        AddLog("TOLERANCE字段值设置完成");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"设置TOLERANCE字段值时出现异常: {ex.Message}");
                }

                AddLog("缝隙检查相关字段处理完成");
            }
            catch (Exception ex)
            {
                AddLog($"添加字段过程中出现异常: {ex.Message}");
            }
        }



        /// <summary>
        /// 改进的临时文件清理方法
        /// </summary>
        private async Task CleanupTemporaryFiles(string tempWorkspace)
        {
            AddLog("正在清理临时文件...");
            await _temporaryWorkspaceStore.CleanupAsync(tempWorkspace, AddLog);
        }

        /// <summary>
        /// 从地图中移除临时图层
        /// </summary>
        private async Task RemoveTemporaryLayersFromMap()
        {
            try
            {
                AddLog("正在检查并移除临时图层...");
                
                // 使用更安全的方式处理图层移除
                await Task.Run(async () =>
                {
                    try
                    {
                        // 等待一段时间，让ArcGIS完成内部处理
                        await Task.Delay(2000);
                        
                        await QueuedTask.Run(() =>
                        {
                            try
                            {
                                // 检查MapView和Map是否有效
                                var mapView = MapView.Active;
                                if (mapView == null)
                                {
                                    AddLog("当前没有活动的地图视图");
                                    return;
                                }

                                var map = mapView.Map;
                                if (map == null)
                                {
                                    AddLog("当前没有活动地图");
                                    return;
                                }

                                // 使用更安全的方式获取图层列表
                                List<Layer> layersToRemove = new List<Layer>();
                                
                                try
                                {
                                    // 分别获取不同类型的图层
                                    var featureLayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                                    
                                    foreach (var layer in featureLayers)
                                    {
                                        try
                                        {
                                            if (layer == null) continue;
                                            
                                            // 使用更安全的方式检查图层名称
                                            string layerName = GetSafeLayerName(layer);
                                            if (string.IsNullOrEmpty(layerName)) continue;

                                            // 检查是否为临时图层
                                            if (IsTemporaryLayer(layerName))
                                            {
                                                layersToRemove.Add(layer);
                                                AddLog($"发现临时图层: {layerName}");
                                            }
                                        }
                                        catch (Exception layerEx)
                                        {
                                            AddLog($"检查图层时出错: {layerEx.Message}");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    AddLog($"获取图层列表失败: {ex.Message}");
                                    return;
                                }

                                // 移除找到的临时图层
                                if (layersToRemove.Count > 0)
                                {
                                    AddLog($"准备移除 {layersToRemove.Count} 个临时图层");
                                    
                                    foreach (var layer in layersToRemove)
                                    {
                                        try
                                        {
                                            if (layer != null && map != null)
                                            {
                                                string layerName = GetSafeLayerName(layer);
                                                
                                                // 使用更安全的移除方式
                                                RemoveLayerSafely(map, layer, layerName);
                                            }
                                        }
                                        catch (Exception layerEx)
                                        {
                                            AddLog($"移除单个图层时出错: {layerEx.Message}");
                                        }
                                    }
                                }
                                else
                                {
                                    AddLog("未发现需要移除的临时图层");
                                }
                            }
                            catch (Exception innerEx)
                            {
                                AddLog($"图层移除过程中出现异常: {innerEx.Message}");
                            }
                        });
                    }
                    catch (Exception taskEx)
                    {
                        AddLog($"执行图层移除任务时出错: {taskEx.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                AddLog($"移除临时图层时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否为临时图层
        /// </summary>
        private bool IsTemporaryLayer(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return false;
            
            return layerName.Contains("dissolved_features") || 
                   layerName.Contains("gaps") || 
                   layerName.Contains("final_gaps") ||
                   layerName.StartsWith("GapCheck_");
        }

        /// <summary>
        /// 安全移除图层
        /// </summary>
        private void RemoveLayerSafely(Map map, Layer layer, string layerName)
        {
            try
            {
                if (map == null || layer == null) return;
                
                // 尝试移除图层
                map.RemoveLayer(layer);
                AddLog($"已从地图中移除临时图层: {layerName ?? "未知图层"}");
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                AddLog($"移除图层时出现COM异常: {layerName ?? "未知图层"} - {comEx.Message}");
                // COM异常通常表示图层已经无效，可以忽略
            }
            catch (System.NullReferenceException nullEx)
            {
                AddLog($"移除图层时出现空引用异常: {layerName ?? "未知图层"} - {nullEx.Message}");
                // 空引用异常表示对象已经被释放，可以忽略
            }
            catch (Exception ex)
            {
                AddLog($"移除图层时出现其他异常: {layerName ?? "未知图层"} - {ex.Message}");
            }
        }
    }
}
