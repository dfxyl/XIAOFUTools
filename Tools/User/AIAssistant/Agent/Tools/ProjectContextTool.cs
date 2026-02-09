using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Tools.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// 工程上下文感知工具 - 获取当前ArcGIS Pro工程信息
    /// </summary>
    public static class ProjectContextTool
    {
        /// <summary>
        /// 获取当前工程的详细信息
        /// </summary>
        public static string GetProjectContext()
        {
            return GetProjectContextAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步获取当前工程的详细信息
        /// </summary>
        public static async Task<string> GetProjectContextAsync()
        {
            try
            {
                // 使用QueuedTask.Run确保在ArcGIS主线程上执行
                return await QueuedTask.Run(BuildProjectContextCore);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取工程上下文失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                return $"获取工程信息时出错: {ex.Message}";
            }
        }

        /// <summary>
        /// 获取详细的工程上下文(用于系统提示词)
        /// </summary>
        public static string GetSimplifiedProjectContext()
        {
            return GetSimplifiedProjectContextAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步获取详细的工程上下文(用于系统提示词)
        /// </summary>
        public static async Task<string> GetSimplifiedProjectContextAsync()
        {
            try
            {
                // 使用QueuedTask.Run确保在ArcGIS主线程上执行
                return await QueuedTask.Run(BuildSimplifiedProjectContextCore);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取工程上下文失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                return $"获取工程信息时出错: {ex.Message}";
            }
        }

        private static string BuildProjectContextCore()
        {
            var project = Project.Current;
            if (project == null)
            {
                return "当前没有打开的ArcGIS Pro工程。";
            }

            var context = new StringBuilder();
            context.AppendLine("## 当前工程信息");
            context.AppendLine();

            context.AppendLine($"**工程名称**: {project.Name}");
            context.AppendLine($"**工程路径**: {project.URI}");
            context.AppendLine($"**默认地理数据库**: {project.DefaultGeodatabasePath}");
            context.AppendLine($"**默认工具箱**: {project.DefaultToolboxPath}");
            context.AppendLine();

            var maps = MapView.Active?.Map;
            if (maps != null)
            {
                context.AppendLine("### 当前地图");
                context.AppendLine($"**地图名称**: {maps.Name}");

                var spatialRef = maps.SpatialReference;
                if (spatialRef != null)
                {
                    context.AppendLine($"**坐标系**: {spatialRef.Name} (WKID: {spatialRef.Wkid})");
                }

                var layers = maps.GetLayersAsFlattenedList();
                if (layers.Count > 0)
                {
                    context.AppendLine($"**图层数量**: {layers.Count}");
                    context.AppendLine("**图层列表**:");
                    foreach (var layer in layers.Take(10))
                    {
                        context.AppendLine($"  - {layer.Name} ({layer.GetType().Name})");
                    }
                    if (layers.Count > 10)
                    {
                        context.AppendLine($"  ... 还有 {layers.Count - 10} 个图层");
                    }
                }
                else
                {
                    context.AppendLine("**图层**: 无");
                }
                context.AppendLine();
            }
            else
            {
                context.AppendLine("### 当前地图");
                context.AppendLine("当前没有打开的地图视图。");
                context.AppendLine();
            }

            var items = project.GetItems<Item>();
            if (items.Any())
            {
                context.AppendLine("### 工程项");
                var itemGroups = items.GroupBy(i => i.TypeID);
                foreach (var group in itemGroups)
                {
                    context.AppendLine($"**{group.Key}**: {group.Count()} 个");
                }
            }

            return context.ToString();
        }

        private static string BuildSimplifiedProjectContextCore()
        {
            var project = Project.Current;
            if (project == null)
            {
                return "当前没有打开ArcGIS Pro工程。";
            }

            var context = new StringBuilder();
            context.AppendLine($"工程名称: {project.Name}");
            context.AppendLine($"工程路径: {project.URI}");
            context.AppendLine($"默认地理数据库: {project.DefaultGeodatabasePath}");

            var mapView = MapView.Active;
            if (mapView != null && mapView.Map != null)
            {
                var map = mapView.Map;
                context.AppendLine();
                context.AppendLine($"当前地图: {map.Name}");

                if (map.SpatialReference != null)
                {
                    var sr = map.SpatialReference;
                    context.AppendLine($"坐标系: {sr.Name} (WKID: {sr.Wkid})");
                    context.AppendLine($"单位: {sr.Unit.Name}");
                }

                var extent = mapView.Extent;
                if (extent != null)
                {
                    context.AppendLine($"当前范围: X({extent.XMin:F2} ~ {extent.XMax:F2}), Y({extent.YMin:F2} ~ {extent.YMax:F2})");
                }

                var layers = map.GetLayersAsFlattenedList();
                if (layers.Count > 0)
                {
                    context.AppendLine();
                    context.AppendLine($"图层总数: {layers.Count}");
                    context.AppendLine("图层列表:");

                    foreach (var layer in layers.Take(15))
                    {
                        var layerType = layer.GetType().Name.Replace("Layer", "");
                        var visible = layer.IsVisible ? "可见" : "隐藏";

                        string dataSource = "";
                        if (layer is FeatureLayer featureLayer)
                        {
                            var fc = featureLayer.GetFeatureClass();
                            if (fc != null)
                            {
                                var shapeType = fc.GetDefinition().GetShapeType();
                                var count = fc.GetCount();
                                dataSource = $" [{shapeType}, {count}要素]";
                            }
                        }

                        context.AppendLine($"  - {layer.Name} ({layerType}, {visible}){dataSource}");
                    }

                    if (layers.Count > 15)
                    {
                        context.AppendLine($"  ... 还有 {layers.Count - 15} 个图层");
                    }
                }
                else
                {
                    context.AppendLine("当前地图没有图层");
                }

                var selection = mapView.Map.GetSelection();
                if (selection != null && selection.Count > 0)
                {
                    context.AppendLine();
                    context.AppendLine($"当前选中: {selection.Count} 个图层有选中要素");
                    var selectionDict = selection.ToDictionary();
                    int count = 0;
                    foreach (var kvp in selectionDict)
                    {
                        if (count >= 5)
                        {
                            break;
                        }
                        context.AppendLine($"  - {kvp.Key.Name}: {kvp.Value.Count} 个要素");
                        count++;
                    }
                }
            }
            else
            {
                context.AppendLine("当前没有打开的地图视图");
            }

            return context.ToString();
        }
    }
}
