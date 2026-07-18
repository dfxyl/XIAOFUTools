using XIAOFUTools.Features.DataManagement.ProtocolLineExtract.Infrastructure;

namespace XIAOFUTools.Tests.ProtocolLineExtract;

public sealed class ProtocolLineTemporaryWorkspaceStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_CreatesAndDeletesTemporaryWorkspace()
    {
        var store = new ProtocolLineTemporaryWorkspaceStore();
        var workspace = store.CreateWorkspace();

        try
        {
            Assert.True(Directory.Exists(workspace));
            store.DeleteIfExists(workspace);
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
