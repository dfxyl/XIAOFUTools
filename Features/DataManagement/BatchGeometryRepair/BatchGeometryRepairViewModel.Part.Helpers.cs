using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.BatchGeometryRepair
{
    internal partial class BatchGeometryRepairViewModel
    {

        /// <summary>
        /// 为单个图层修复几何
        /// </summary>
        private async Task RepairGeometryForLayer(FeatureLayer layer)
        {
            await QueuedTask.Run(async () =>
            {
                try
                {
                    // 使用ArcGIS Pro的修复几何地理处理工具
                    var parameters = Geoprocessing.MakeValueArray(
                        layer,
                        "DELETE_NULL"  // 删除空几何
                    );

                    var result = await Geoprocessing.ExecuteToolAsync(
                        "RepairGeometry_management",
                        parameters,
                        null,
                        null,
                        null,
                        GPExecuteToolFlags.GPThread);
                    if (result.IsFailed)
                    {
                        throw new Exception($"修复几何失败: {string.Join(", ", result.Messages.Select(m => m.Text))}");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"修复几何失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 处理图层信息属性变化事件
        /// </summary>
        private void LayerInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 当IsSelected属性改变时，通知CanProcess属性也发生了变化
            if (e.PropertyName == nameof(LayerGeometryInfo.IsSelected))
            {
                NotifyPropertyChanged(() => CanProcess);
            }
        }
    }
}
