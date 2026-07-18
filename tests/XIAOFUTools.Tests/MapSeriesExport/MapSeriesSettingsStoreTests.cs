using XIAOFUTools.Features.Cartography.MapSeriesExport.Core;
using XIAOFUTools.Features.Cartography.MapSeriesExport.Infrastructure;

namespace XIAOFUTools.Tests.MapSeriesExport;

[Trait("Category", "Integration")]
public sealed class MapSeriesSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "XIAOFUTools.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_PreservesExistingJsonContract()
    {
        var path = Path.Combine(_directory, "MapSeriesSettings.json");
        var store = new MapSeriesSettingsStore(path);
        store.Save(new CoordinateTableSettings
        {
            EnableCoordinateTable = true,
            LayerName = "宗地图",
            TitleText = "测试标题",
            IntersectLayerName = "地类图层",
            EdgeLabelDecimal = 3
        });

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.True(loaded!.EnableCoordinateTable);
        Assert.Equal("宗地图", loaded.LayerName);
        Assert.Equal("测试标题", loaded.TitleText);
        Assert.Equal("地类图层", loaded.IntersectLayerName);
        Assert.Equal(3, loaded.EdgeLabelDecimal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
