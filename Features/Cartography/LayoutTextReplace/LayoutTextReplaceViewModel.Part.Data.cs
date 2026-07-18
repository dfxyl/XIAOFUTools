using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Cartography.LayoutTextReplace
{
    public partial class LayoutTextReplaceViewModel
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

                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        Layouts.Clear();
                        foreach (var layout in layouts)
                        {
                            Layouts.Add(new LayoutSelectItem { Name = layout.Name, IsSelected = true });
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }
    }
}
