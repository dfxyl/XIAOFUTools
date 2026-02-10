using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.AreaCalculator
{
    /// <summary>
    /// 计算面积停靠窗格
    /// </summary>
    internal class AreaCalculatorDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_AreaCalculatorDockPane";
        private AreaCalculatorDockPaneView _view;
        private string _pendingLayerName;

        protected AreaCalculatorDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            _view = new AreaCalculatorDockPaneView();

            if (!string.IsNullOrWhiteSpace(_pendingLayerName))
            {
                _view.ApplyPreferredLayerName(_pendingLayerName);
            }

            return _view;
        }

        /// <summary>
        /// 显示停靠窗格
        /// </summary>
        internal static void Show(string preferredLayerName = null)
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);

            if (pane is AreaCalculatorDockPane areaPane)
            {
                areaPane.ApplyPreferredLayerName(preferredLayerName);
            }

            pane?.Activate();
        }

        private void ApplyPreferredLayerName(string layerName)
        {
            _pendingLayerName = layerName;
            _view?.ApplyPreferredLayerName(layerName);
        }
    }
}
