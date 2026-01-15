using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Framework.Dialogs;
using System.IO;
using System.Linq;
using System;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.Analysis.MatchSymbology
{
    internal class MatchSymbologyFXDLBMButton : Button
    {
        // 相对路径字段，指向符号库位置
        private string _relativePath = @"Data\符号库\";

        protected override async void OnClick()
        {
            // 检查是否有足够的授权执行此操作
            if (!AuthorizationChecker.CheckAuthorizationWithPrompt("FXDLBM匹配符号功能")) return;

            // 确认当前有活动的地图视图
            if (MapView.Active == null) return;

            // 获取当前选中的图层，确认只选中了一个图层
            var selectedLayers = MapView.Active.GetSelectedLayers();
            if (selectedLayers == null || selectedLayers.Count != 1) return;

            // 检查选中的图层是否为要素图层
            var selectedLayer = selectedLayers[0] as FeatureLayer;
            if (selectedLayer == null) return;

            // 获取选中图层的 URI
            string layerUri = selectedLayer.URI;

            // 获取当前程序集的位置
            string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

            // 获取安装目录
            string installPath = Path.GetDirectoryName(assemblyLocation);

            // 组合完整路径
            string fullStylePath = Path.Combine(installPath, _relativePath, "地类符号.stylx");

            // 确认符号库文件是否存在
            if (!File.Exists(fullStylePath)) return;

            // 在主线程上执行字段检查
            bool fieldExists =
            await QueuedTask.Run(() =>
            {
                // 获取选中图层的字段
                var fields = selectedLayer.GetTable().GetDefinition().GetFields();

                // 检查字段是否存在于选中的图层中
                return fields.Any(field => field.Name.Equals("FXDLBM", StringComparison.OrdinalIgnoreCase));
            });
            if (!fieldExists)
            {
                // 使用 ArcGIS Pro 的消息框
                MessageBox.Show("字段 'FXDLBM' 不存在于选中的图层中。");
                return;
            }


            // 构造 Geoprocessing 工具的参数
            var parameters = Geoprocessing.MakeValueArray(
                layerUri,      // 图层路径
                "FXDLBM",        // 字段名，默认为 FXDLBM，且不区分大小写
                fullStylePath  // 符号库的完整路径
            );

            // 异步执行符号匹配工具
            var result = await Geoprocessing.ExecuteToolAsync("MatchLayerSymbologyToAStyle_management", parameters);

            // 如果工具执行失败则直接返回
            if (result.IsFailed) return;
        }
    }
}
