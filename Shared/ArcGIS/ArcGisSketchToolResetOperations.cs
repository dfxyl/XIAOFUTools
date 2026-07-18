#nullable enable

using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Shared
{
    internal sealed class ArcGisSketchToolResetOperations : ISketchToolResetOperations
    {
        public static ArcGisSketchToolResetOperations Instance { get; } = new();

        private ArcGisSketchToolResetOperations()
        {
        }

        public Task CancelDrawingAsync()
        {
            MapView.Active?.CancelDrawing();
            return Task.CompletedTask;
        }

        public Task SetCurrentToolAsync(string? toolId)
            => FrameworkApplication.SetCurrentToolAsync(toolId);
    }
}
