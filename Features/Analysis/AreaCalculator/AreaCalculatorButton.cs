using System;
using System.Collections;
using System.Linq;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.AreaCalculator
{
    /// <summary>
    /// 计算面积按钮
    /// </summary>
    internal class AreaCalculatorButton : Button
    {
        protected override void OnClick()
        {
            try
            {

                var contextOptions = ResolveContextOptions();
                AreaCalculatorDockPane.Show(contextOptions);
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }

        private static AreaCalculatorContextOptions ResolveContextOptions()
        {
            var contextLayer = GetContextFeatureLayer() ?? GetSelectedFeatureLayer();
            if (contextLayer == null)
            {
                return null;
            }

            return new AreaCalculatorContextOptions
            {
                PreferredLayerName = contextLayer.Name,
                PreferredLayerUri = contextLayer.URI
            };
        }

        private static FeatureLayer GetSelectedFeatureLayer()
        {
            return MapView.Active?.GetSelectedLayers()?.OfType<FeatureLayer>().FirstOrDefault();
        }

        private static FeatureLayer GetContextFeatureLayer()
        {
            var directFeatureLayer = FrameworkApplication.ContextMenuDataContextAs<FeatureLayer>();
            if (directFeatureLayer != null)
            {
                return directFeatureLayer;
            }

            var directMapMember = FrameworkApplication.ContextMenuDataContextAs<MapMember>();
            if (directMapMember is FeatureLayer featureLayerFromMapMember)
            {
                return featureLayerFromMapMember;
            }

            var context = FrameworkApplication.ContextMenuDataContext;
            if (context is FeatureLayer featureLayer)
            {
                return featureLayer;
            }

            if (context is MapMember mapMember && mapMember is FeatureLayer mapMemberFeatureLayer)
            {
                return mapMemberFeatureLayer;
            }

            if (context is IEnumerable collection)
            {
                foreach (var item in collection)
                {
                    if (item is FeatureLayer itemFeatureLayer)
                    {
                        return itemFeatureLayer;
                    }

                    if (item is MapMember itemMapMember && itemMapMember is FeatureLayer itemMapMemberFeatureLayer)
                    {
                        return itemMapMemberFeatureLayer;
                    }
                }
            }

            return null;
        }
    }
}
