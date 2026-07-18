using System;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.ProtocolLineExtract
{
    /// <summary>
    /// 提取协议线停靠窗格
    /// </summary>
    internal class ProtocolLineExtractDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_ProtocolLineExtractDockPane";

        protected ProtocolLineExtractDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new ProtocolLineExtractDockPaneView();
        }

        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
