using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.BoundaryPointGenerator
{
    internal partial class BoundaryPointGeneratorDockPaneViewModel
    {

        /// 确定命令是否可以执行
        /// </summary>
        private bool CanExecute()
        {
            return SelectedPolygonLayer != null && !string.IsNullOrEmpty(OutputPath) && !IsProcessing;
        }

        /// <summary>
        /// 执行生成四至坐标点操作
        /// </summary>
        private async void Execute()
        {
            // 防止重复执行
            if (IsProcessing)
            {
                LogWarning("工具正在运行中，请等待完成后再次执行");
                return;
            }

            // 重置取消标志
            CancelRequested = false;
            // 设置处理状态
            IsProcessing = true;

            StatusMessage = "正在处理...";
            ClearLog();
            // 重置进度条
            Progress = 0;
            IsProgressIndeterminate = true;

            try
            {
                LogInfo("开始生成四至坐标点...");

                await QueuedTask.Run(async () =>
                {
                    try
                    {
                        // 检查取消请求
                        if (CancelRequested)
                        {
                            LogWarning("操作已取消");
                            return;
                        }

                        // 创建输出要素类
                        var outputFeatureClassPath = await CreateOutputFeatureClass();
                        if (outputFeatureClassPath == null)
                        {
                            LogError("创建输出要素类失败");
                            return;
                        }

                        LogInfo($"成功创建输出要素类: {outputFeatureClassPath}");

                        // 处理面要素，生成四至坐标点
                        await ProcessPolygonFeatures(outputFeatureClassPath);

                        if (!CancelRequested)
                        {
                            LogInfo("四至坐标点生成完成！");

                            PresentationServices.UiThread.InvokeOrRun(() =>
                            {
                                StatusMessage = "处理完成！";
                                Progress = 100;
                                IsProgressIndeterminate = false;
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理过程中发生错误: {ex.Message}");
                        PresentationServices.UiThread.InvokeOrRun(() =>
                        {
                            StatusMessage = $"处理失败: {ex.Message}";
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"执行失败: {ex.Message}");
                StatusMessage = $"执行失败: {ex.Message}";
            }
            finally
            {
                // 重置处理状态
                IsProcessing = false;
                IsProgressIndeterminate = false;
            }
        }
    }
}
