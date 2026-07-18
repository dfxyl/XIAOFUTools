#nullable enable

using System.Threading.Tasks;

namespace XIAOFUTools.Shared
{
    internal interface ISketchToolResetOperations
    {
        Task CancelDrawingAsync();

        Task SetCurrentToolAsync(string? toolId);
    }

    internal static class SketchToolResetWorkflow
    {
        public static async Task ResetAsync(ISketchToolResetOperations operations)
        {
            await operations.CancelDrawingAsync();
            await operations.SetCurrentToolAsync(null);
            await operations.SetCurrentToolAsync("esri_mapping_exploreTool");
        }
    }
}
