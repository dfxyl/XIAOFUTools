using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Analysis.AreaCalculator
{
    /// <summary>
    /// Context options passed from command invokers (for example layer context menu).
    /// </summary>
    internal class AreaCalculatorContextOptions
    {
        public string PreferredLayerName { get; set; }
        public string PreferredLayerUri { get; set; }
    }

    /// <summary>
    /// 计算面积停靠窗格
    /// </summary>
    internal class AreaCalculatorDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_AreaCalculatorDockPane";
        private AreaCalculatorDockPaneView _view;
        private AreaCalculatorContextOptions _pendingContextOptions;

        protected AreaCalculatorDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            _view = new AreaCalculatorDockPaneView();

            if (_pendingContextOptions != null)
            {
                _view.ApplyContextOptions(_pendingContextOptions);
            }

            return _view;
        }

        internal static void Show(AreaCalculatorContextOptions contextOptions = null)
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);

            if (pane is AreaCalculatorDockPane areaPane)
            {
                areaPane.ApplyContextOptions(contextOptions);
            }

            pane?.Activate();
        }

        private void ApplyContextOptions(AreaCalculatorContextOptions contextOptions)
        {
            _pendingContextOptions = contextOptions;
            _view?.ApplyContextOptions(contextOptions);
        }
    }
}
