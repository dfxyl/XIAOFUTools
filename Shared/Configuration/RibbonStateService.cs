using ArcGIS.Desktop.Framework;

namespace XIAOFUTools.Shared.Configuration
{
    internal static class RibbonStateService
    {
        public const string GisToolPocketTabVisibleStateId = "XIAOFUTools_GisToolPocketTabVisible_State";

        public static void SetState(string stateId, bool isActive)
        {
            if (isActive)
                FrameworkApplication.State.Activate(stateId);
            else
                FrameworkApplication.State.Deactivate(stateId);
        }
    }
}
