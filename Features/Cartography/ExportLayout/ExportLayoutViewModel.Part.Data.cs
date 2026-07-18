using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Cartography.ExportLayout
{
    public partial class ExportLayoutViewModel
    {

        /// <summary>
        /// 加载布局列表
        /// </summary>
        private async void LoadLayouts()
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var project = Project.Current;
                    if (project == null) return;

                    var layouts = project.GetItems<LayoutProjectItem>();
                    
                    foreach (var layout in layouts)
                    {
                        PresentationServices.UiThread.InvokeOrRun(() =>
                        {
                            Layouts.Add(new LayoutItem { Name = layout.Name, IsSelected = true });
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取文件扩展名
        /// </summary>
        private string GetFileExtension()
        {
            return SelectedFormat.ToLower() switch
            {
                "pdf" => "pdf",
                "tif" => "tif",
                "tiff" => "tif",
                "geotiff" => "tif",
                "jpg" => "jpg",
                "jpeg" => "jpg",
                "png" => "png",
                _ => "pdf"
            };
        }

        /// <summary>
        /// 获取布局中第一个有效的地图框架名称
        /// </summary>
        private string GetValidMapFrameName(Layout layout)
        {
            try
            {
                // 获取布局中的所有元素，然后筛选出地图框架
                var allElements = layout.GetElements();
                var mapFrames = allElements.OfType<MapFrame>().ToList();

                System.Diagnostics.Debug.WriteLine($"布局中找到 {mapFrames.Count} 个地图框架");

                foreach (var mapFrame in mapFrames)
                {
                    System.Diagnostics.Debug.WriteLine($"检查地图框架: {mapFrame.Name}");

                    // 检查地图框架是否有有效的地图
                    if (mapFrame.Map == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  地图框架 {mapFrame.Name} 没有关联地图");
                        continue;
                    }

                    // 检查地图是否有坐标系统
                    if (mapFrame.Map.SpatialReference == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  地图框架 {mapFrame.Name} 的地图没有坐标系统");
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"  地图框架 {mapFrame.Name} 有效，坐标系统: {mapFrame.Map.SpatialReference.Name}");
                    return mapFrame.Name;
                }

                System.Diagnostics.Debug.WriteLine("没有找到有效的地图框架");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取地图框架时出错: {ex.Message}");
                return null;
            }
        }
    }
}
