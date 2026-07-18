using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Editing.Boundary.BoundaryPointLineGenerator
{
    internal partial class BoundaryPointLineGeneratorDockPaneViewModel
    {

        private string GetProjectGDBPath()
        {
            try { return Project.Current?.DefaultGeodatabasePath; }
            catch { return null; }
        }

        private void LoadPolygonLayers()
        {
            // 使用通用接口获取面图层列表
            Task.Run(async () =>
            {
                try
                {
                    var list = await LayerUtils.GetPolygonLayersAsync();
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        PolygonLayers.Clear();
                        foreach (var fl in list) PolygonLayers.Add(fl);
                        if (PolygonLayers.Count > 0) SelectedPolygonLayer = PolygonLayers[0];
                        StatusMessage = PolygonLayers.Count > 0 ? "请选择参数后开始生成" : "未找到面图层";
                    });
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.InvokeOrRun(() => StatusMessage = $"加载图层失败: {ex.Message}");
                }
            });
        }

        private void LoadAvailableFields()
        {
            AvailableFields.Clear();
            if (SelectedPolygonLayer == null) return;
            QueuedTask.Run(() =>
            {
                try
                {
                    using var table = SelectedPolygonLayer.GetTable();
                    var fields = table?.GetDefinition()?.GetFields();
                    if (fields == null) return;
                    var texts = fields.Where(f => f.FieldType == FieldType.String).Select(f => f.Name).ToList();
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        foreach (var n in texts) AvailableFields.Add(n);
                        // 尝试默认匹配
                        SelectedCodeFieldName = AvailableFields.FirstOrDefault(n => n.Equals("ZDZHDM", StringComparison.OrdinalIgnoreCase) || n.Contains("ZDDM", StringComparison.OrdinalIgnoreCase)) ?? AvailableFields.FirstOrDefault();
                    });
                }
                catch (Exception ex)
                {
                    LogError($"读取字段失败: {ex.Message}");
                }
            });
        }

        private static string GetStringSafe(Feature feature, string fieldName)
        {
            try
            {
                if (feature == null || string.IsNullOrWhiteSpace(fieldName)) return null;
                var v = feature[fieldName];
                return v?.ToString();
            }
            catch { return null; }
        }
    }
}
