using XIAOFUTools.Features.Analysis.DataPivot.Infrastructure;

namespace XIAOFUTools.Tests.DataPivot;

public sealed class PivotWorkspaceResolverTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Resolver_PrefersExistingOutputGeodatabaseAndFallsBackToProjectDefault()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-pivot-workspace-{Guid.NewGuid():N}");
        var outputGeodatabase = Path.Combine(root, "output.gdb");
        var projectGeodatabase = Path.Combine(root, "project.gdb");
        var resolver = new PivotWorkspaceResolver();

        try
        {
            Directory.CreateDirectory(outputGeodatabase);
            Directory.CreateDirectory(projectGeodatabase);

            Assert.Equal(
                outputGeodatabase,
                resolver.ResolveTemporaryWorkspacePath(outputGeodatabase, projectGeodatabase));

            Directory.Delete(outputGeodatabase);
            Assert.Equal(
                projectGeodatabase,
                resolver.ResolveTemporaryWorkspacePath(outputGeodatabase, projectGeodatabase));
            Assert.False(resolver.DirectoryExists(outputGeodatabase));
            Assert.True(resolver.DirectoryExists(projectGeodatabase));
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
