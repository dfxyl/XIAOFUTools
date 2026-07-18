using System.Collections.Generic;
using System.Threading.Tasks;
using XIAOFUTools.Shared;
using Xunit;

namespace XIAOFUTools.Tests.InternetTileDownload;

public class SketchToolResetWorkflowTests
{
    [Fact]
    public async Task ResetAsync_CancelsDrawingThenRestoresExploreTool()
    {
        var operations = new FakeSketchToolResetOperations();

        await SketchToolResetWorkflow.ResetAsync(operations);

        Assert.Equal(
            new[] { "CancelDrawing", "SetTool:", "SetTool:esri_mapping_exploreTool" },
            operations.Calls);
    }

    private sealed class FakeSketchToolResetOperations : ISketchToolResetOperations
    {
        public List<string> Calls { get; } = new();

        public Task CancelDrawingAsync()
        {
            Calls.Add("CancelDrawing");
            return Task.CompletedTask;
        }

        public Task SetCurrentToolAsync(string? toolId)
        {
            Calls.Add($"SetTool:{toolId}");
            return Task.CompletedTask;
        }
    }
}
