using XIAOFUTools.Features.DataManagement.BatchAddData.Infrastructure;

namespace XIAOFUTools.Tests.BatchAddData;

public sealed class BatchAddDataSearchPathValidatorTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Validator_RecognizesAccessibleDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-batch-add-{Guid.NewGuid():N}");
        var validator = new BatchAddDataSearchPathValidator();

        try
        {
            Directory.CreateDirectory(root);

            Assert.True(validator.IsAccessible(root));
            Assert.False(validator.IsAccessible(Path.Combine(root, "missing")));
            Assert.False(validator.IsAccessible(string.Empty));
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
