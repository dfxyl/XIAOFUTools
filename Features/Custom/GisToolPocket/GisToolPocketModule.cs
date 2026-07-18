using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Features.Custom.GisToolPocket.Presentation;
using XIAOFUTools.Shared.Configuration;

namespace XIAOFUTools.Features.Custom.GisToolPocket
{
    internal sealed class GisToolPocketModule : Module
    {
        protected override bool Initialize()
        {
            RibbonStateService.SetState(
                RibbonStateService.GisToolPocketTabVisibleStateId,
                SettingsManager.Settings.GisToolPocket.ShowRibbonTab);
            ToolboxMenuSlotService.RefreshRibbon();
            return base.Initialize();
        }

        protected override bool CanUnload()
        {
            return true;
        }
    }
}
