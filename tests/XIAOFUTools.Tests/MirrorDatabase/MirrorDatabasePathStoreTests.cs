using XIAOFUTools.Features.DataManagement.MirrorDatabase.Infrastructure;

namespace XIAOFUTools.Tests.MirrorDatabase;

public sealed class MirrorDatabasePathStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_RecognizesExistingDatabaseDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-mirror-database-{Guid.NewGuid():N}");
        var database = Path.Combine(root, "source.gdb");
        var store = new MirrorDatabasePathStore();

        try
        {
            Directory.CreateDirectory(database);

            Assert.True(store.DirectoryExists(database));
            Assert.False(store.DirectoryExists(Path.Combine(root, "missing.gdb")));
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
