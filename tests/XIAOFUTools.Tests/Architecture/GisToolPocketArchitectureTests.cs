namespace XIAOFUTools.Tests.Architecture;

public sealed class GisToolPocketArchitectureTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

    private static readonly string FeatureRoot = Path.Combine(
        RepositoryRoot,
        "Features",
        "Custom",
        "GisToolPocket");

    [Fact]
    public void LayerNamespaces_MatchFeatureDirectories()
    {
        AssertLayerNamespace("Core");
        AssertLayerNamespace("Application");
        AssertLayerNamespace("Infrastructure");
        AssertLayerNamespace("Presentation");
    }

    [Fact]
    public void LayerDependencies_RespectArchitectureDirection()
    {
        var coreSources = ReadLayer("Core");
        var infrastructureSources = ReadLayer("Infrastructure");
        var presentationSources = ReadLayer("Presentation");

        Assert.DoesNotContain(coreSources, source => source.Contains(".Infrastructure", StringComparison.Ordinal));
        Assert.DoesNotContain(coreSources, source => source.Contains(".Application", StringComparison.Ordinal));
        Assert.DoesNotContain(coreSources, source => source.Contains(".Presentation", StringComparison.Ordinal));
        Assert.DoesNotContain(infrastructureSources, source => source.Contains(".Application", StringComparison.Ordinal));
        Assert.DoesNotContain(infrastructureSources, source => source.Contains(".Presentation", StringComparison.Ordinal));
        Assert.DoesNotContain(presentationSources, source => source.Contains(".Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void ConfigDaml_GisToolPocketModuleHasNoPatchArtifacts()
    {
        var config = File.ReadAllText(Path.Combine(RepositoryRoot, "Config.daml"));

        Assert.DoesNotContain("+    <insertModule id=\"GIS_Toolbox_Module\"", config, StringComparison.Ordinal);
        Assert.Contains(
            "<insertModule id=\"GIS_Toolbox_Module\" className=\"XIAOFUTools.Features.Custom.GisToolPocket.GisToolPocketModule\"",
            config,
            StringComparison.Ordinal);
    }

    private static void AssertLayerNamespace(string layer)
    {
        var expectedNamespace = $"namespace XIAOFUTools.Features.Custom.GisToolPocket.{layer}";
        var files = Directory.EnumerateFiles(Path.Combine(FeatureRoot, layer), "*.cs", SearchOption.AllDirectories);

        Assert.All(files, file => Assert.Contains(expectedNamespace, File.ReadAllText(file), StringComparison.Ordinal));
    }

    private static IEnumerable<string> ReadLayer(string layer)
    {
        return Directory.EnumerateFiles(Path.Combine(FeatureRoot, layer), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText);
    }
}
