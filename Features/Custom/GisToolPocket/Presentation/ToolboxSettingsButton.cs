using ArcGIS.Desktop.Framework.Contracts;

using XIAOFUTools.Features.Custom.GisToolPocket.Application;
using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Presentation
{
    internal sealed class ToolboxSettingsButton : Button
    {
        protected override void OnClick()
        {
            var window = new ToolboxManagerWindow();
            window.ShowDialog();
        }
    }
}
