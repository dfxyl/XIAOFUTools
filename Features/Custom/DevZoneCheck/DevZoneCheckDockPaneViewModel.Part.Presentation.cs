using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;

namespace XIAOFUTools.Features.Custom.DevZoneCheck
{
    internal partial class DevZoneCheckDockPaneViewModel
    {

        public void RefreshLayers()
        {
            QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                if (map == null) return;

                var layers = map.GetLayersAsFlattenedList()
                    .OfType<FeatureLayer>()
                    .Where(l =>
                    {
                        try
                        {
                            var fc = l.GetFeatureClass();
                            if (fc == null) return false;
                            var shapeType = fc.GetDefinition().GetShapeType();
                            return shapeType == GeometryType.Polygon;
                        }
                        catch { return false; }
                    })
                    .ToList();

                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    PolygonLayers.Clear();
                    foreach (var layer in layers)
                    {
                        PolygonLayers.Add(layer);
                    }
                });
            });
        }

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] {message}\n";
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] 错误: {message}\n";
        }

        private void ClearLog()
        {
            LogContent = "";
        }

        private void ShowHelp()
        {
            var helpText = @"【开发区整合优化核查工具】

功能说明:
基于园区红线，对开发区进行底线类、节约集约类、发展空间类核查分析。
所有面积基于椭球面计算。

参数说明:
1. 园区红线: 开发区/园区范围图层，可选分组字段进行分组统计
2. 底线类核查:
   - 城镇开发边界: 核查园区是否全部位于开发边界内
   - 永久基本农田: 核查园区是否压占永久基本农田
   - 生态保护红线: 核查园区是否压占生态保护红线

3. 节约集约类核查:
   - 变更调查数据: 计算现状建设用地、工业用地等面积及工业用地率
     (建设用地代码: 05/06/07/08/09/10(除1006)/1109)
     (工业用地:0601, 采矿:0602, 盐田:0603, 仓储:0508)
   - 批而未供数据: 计算批而未供率
   - 已批建设用地/已供应数据: 计算供应情况
   - 闲置土地数据: 计算闲置土地率

4. 发展空间类核查:
   - 国土空间规划: 计算规划工业用地率
     (工矿仓储用地代码: 10/11开头)
   - 举证数据: 用于计算尚可供应年限

指标计算公式:
- 现状工业用地率 = 现状工矿仓储用地面积/现状建设用地面积×100%
- 批而未供率 = 批而未供面积/已批准建设用地面积×100%
- 闲置土地率 = 闲置土地面积/已供应建设用地面积×100%
- 规划工业用地率 = 规划工矿仓储用地面积/规划建设用地面积×100%
- 尚可供应年限 = (园区范围-已供应+举证)/(年均供应量)";

            PresentationServices.Dialogs.Show(helpText, "帮助", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
