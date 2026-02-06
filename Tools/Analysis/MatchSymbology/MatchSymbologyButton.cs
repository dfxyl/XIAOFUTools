using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.Analysis.MatchSymbology
{
    internal class MatchSymbologyButton : Button
    {
        protected override void OnClick()
        {
            // 检查授权状态
            if (!AuthorizationChecker.CheckAuthorizationWithPrompt("匹配符号功能"))
            {
                return; // 授权无效时，退出操作
            }

            // 检查活动地图视图
            if (MapView.Active == null) return;

            // 获取当前活动视图中选中的单个图层
            var selectedLayers = MapView.Active.GetSelectedLayers();

            // 如果没有选中图层或者选中多个图层则返回
            if (selectedLayers.Count != 1) return;

            // 获取选中的图层
            var selectedLayer = selectedLayers[0] as FeatureLayer;
            if (selectedLayer == null) return;

            // 获取选中图层的 URI
            string layerUri = selectedLayer.URI; // 使用 selectedLayer.URI 获取 URI

            // 将图层 URI 作为参数传递给工具
            var parameters = Geoprocessing.MakeValueArray(
                layerUri // 传递选中图层的 URI
            );

            // 调用工具界面，而不是直接执行
            Geoprocessing.OpenToolDialog("MatchLayerSymbologyToAStyle_management", parameters);
        }
    }
}
