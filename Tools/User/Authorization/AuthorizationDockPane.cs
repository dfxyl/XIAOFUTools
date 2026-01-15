using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.Authorization
{
    /// <summary>
    /// 授权管理停靠窗格
    /// </summary>
    internal class AuthorizationDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_AuthorizationDockPane";
        private AuthorizationDockPaneViewModel _viewModel;

        protected AuthorizationDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            var view = new AuthorizationDockPaneView();
            _viewModel = new AuthorizationDockPaneViewModel();
            view.DataContext = _viewModel;
            return view;
        }

        /// <summary>
        /// 显示停靠窗格
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
