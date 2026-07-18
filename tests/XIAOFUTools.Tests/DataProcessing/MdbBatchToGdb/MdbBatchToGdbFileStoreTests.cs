using XIAOFUTools.Features.DataManagement.MdbBatchToGdb.Infrastructure;

namespace XIAOFUTools.Tests.DataProcessing.MdbBatchToGdb;

public sealed class MdbBatchToGdbFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_ManagesMdbDiscoveryAndOutputPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-mdb-batch-{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "nested");
        var output = Path.Combine(root, "output", "result.gdb");
        var store = new MdbBatchToGdbFileStore();

        try
        {
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(root, "first.mdb"), "mdb");
            File.WriteAllText(Path.Combine(nested, "second.mdb"), "mdb");

            Assert.Single(store.EnumerateMdbFiles(root, includeSubfolders: false));
            Assert.Equal(2, store.EnumerateMdbFiles(root, includeSubfolders: true).Count);

            store.EnsureDirectory(output);

            Assert.True(store.DirectoryExists(output));
            Assert.True(store.PathExists(output));
            Assert.Equal(Path.Combine("nested", "second.mdb"), store.BuildRelativePath(root, Path.Combine(nested, "second.mdb")));

            store.DeletePath(output);

            Assert.False(store.PathExists(output));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
