using XIAOFUTools.Features.DataManagement.DatabaseBuilder.Infrastructure;

namespace XIAOFUTools.Tests.DatabaseBuilder;

public sealed class DatabaseBuilderFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_FindsAndCopiesAvailableTemplates()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-database-builder-{Guid.NewGuid():N}");
        var templateFolder = Path.Combine(root, "Data", "Excel模板");
        var output = Path.Combine(root, "output");
        var store = new DatabaseBuilderFileStore();

        try
        {
            Directory.CreateDirectory(templateFolder);
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(templateFolder, "建库模板.xls"), "template");

            var templates = store.GetAvailableTemplates(root);
            store.CopyTemplates(templates, output);

            Assert.Single(templates);
            Assert.True(store.FileExists(Path.Combine(output, "建库模板.xls")));
            Assert.False(store.DirectoryExists(Path.Combine(root, "missing")));
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
