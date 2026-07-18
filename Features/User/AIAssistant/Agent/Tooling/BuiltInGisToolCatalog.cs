using System.Collections.Generic;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tools;

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Tooling
{
    internal static class BuiltInGisToolCatalog
    {
        internal static IReadOnlyList<IGISTool> CreateTools()
        {
            return new IGISTool[]
            {
                new WebFetchTool(),
                new ProjectSnapshotTool(),
                new LayerListTool(),
                new LayerSchemaTool(),
                new SelectionSummaryTool(),
                new LayerQueryTool(),
                new FieldProfileTool(),
                new OverlayIntersectSummaryTool(),
                new LayerBufferTool(),
                new LayerClipTool(),
                new CreatePythonToolboxTool()
            };
        }
    }
}
