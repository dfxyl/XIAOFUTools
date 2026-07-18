using XIAOFUTools.Features.Editing.GapCheck.Infrastructure;

namespace XIAOFUTools.Tests.GapCheck;

public sealed class GapCheckTemporaryWorkspaceStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Store_CreatesAndCleansTemporaryWorkspace()
    {
        var store = new GapCheckTemporaryWorkspaceStore();
        var workspace = store.CreateWorkspace();
        var file = Path.Combine(workspace, "intermediate.txt");

        try
        {
            await File.WriteAllTextAsync(file, "temporary");
            await store.CleanupAsync(workspace, _ => { });

            Assert.False(Directory.Exists(workspace));
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }
}
