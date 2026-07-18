using XIAOFUTools.Features.Conversion.FeatureToTxt.Infrastructure;

namespace XIAOFUTools.Tests.FeatureToTxt;

public sealed class FeatureTxtOutputFolderResolverTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Resolver_UsesExistingProjectDirectoryAndValidatesFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-feature-txt-output-{Guid.NewGuid():N}");
        var projectPath = Path.Combine(root, "project.aprx");
        var resolver = new FeatureTxtOutputFolderResolver();

        try
        {
            Directory.CreateDirectory(root);

            var resolution = resolver.ResolveDefaultOutputFolder(projectPath);

            Assert.NotNull(resolution);
            Assert.Equal(root, resolution.Path);
            Assert.Equal("工程位置", resolution.Source);
            Assert.True(resolver.DirectoryExists(root));
            Assert.False(resolver.DirectoryExists(Path.Combine(root, "missing")));
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
