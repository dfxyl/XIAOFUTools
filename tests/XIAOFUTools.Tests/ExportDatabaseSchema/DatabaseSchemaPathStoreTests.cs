using XIAOFUTools.Features.DataManagement.ExportDatabaseSchema.Infrastructure;

namespace XIAOFUTools.Tests.ExportDatabaseSchema;

public sealed class DatabaseSchemaPathStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_RecognizesExistingDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-schema-path-{Guid.NewGuid():N}");
        var store = new DatabaseSchemaPathStore();

        try
        {
            Directory.CreateDirectory(root);

            Assert.True(store.DirectoryExists(root));
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
