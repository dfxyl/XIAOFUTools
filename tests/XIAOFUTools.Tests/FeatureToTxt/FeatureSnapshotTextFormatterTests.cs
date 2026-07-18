using System.Text;
using XIAOFUTools.Features.Conversion.FeatureToTxt.Core;

namespace XIAOFUTools.Tests.FeatureToTxt;

public sealed class FeatureSnapshotTextFormatterTests
{
    [Theory]
    [InlineData("20000", 20000d, "2.0000")]
    [InlineData("2", 20000d, "2.0000")]
    [InlineData("", 25000d, "2.5000")]
    public void Write_NormalizesMappedFieldsAndArea(
        string areaFieldValue,
        double geometryArea,
        string expectedArea)
    {
        var snapshot = CreateSnapshot(areaFieldValue, geometryArea);
        var content = new StringBuilder();

        new FeatureSnapshotTextFormatter().Write(
            snapshot,
            content,
            7,
            new FeatureToTxtExportConfiguration("J", 2, true, false, false, false));

        var firstLine = content.ToString().Split(
            new[] { "\r\n", "\n" },
            StringSplitOptions.RemoveEmptyEntries)[0];
        Assert.Equal($"5,{expectedArea},DK007,测试地块,面,H-01,耕地,0101,@", firstLine);
    }

    [Fact]
    public void Write_UsesFeatureIndexWhenNumberAndNameAreMissing()
    {
        var snapshot = new FeatureExportSnapshot(
            8,
            10000,
            CreateSquare(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var content = new StringBuilder();

        new FeatureSnapshotTextFormatter().Write(
            snapshot,
            content,
            8,
            new FeatureToTxtExportConfiguration("J", 2, false, false, false, false));

        Assert.StartsWith("4,1.0000,8,地块8,面,,,,@", content.ToString(), StringComparison.Ordinal);
    }

    private static FeatureExportSnapshot CreateSnapshot(string areaFieldValue, double geometryArea)
    {
        return new FeatureExportSnapshot(
            7,
            geometryArea,
            CreateSquare(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MJ"] = areaFieldValue,
                ["BH"] = "DK007",
                ["NAME"] = "测试地块",
                ["TFH"] = "H-01",
                ["LANDUSE"] = "耕地",
                ["CODE"] = "0101"
            });
    }

    private static IReadOnlyList<FeatureCoordinateRing> CreateSquare()
    {
        return new[]
        {
            new FeatureCoordinateRing(new[]
            {
                new FeatureCoordinate(100, 20),
                new FeatureCoordinate(101, 20),
                new FeatureCoordinate(101, 21),
                new FeatureCoordinate(100, 21),
                new FeatureCoordinate(100, 20)
            })
        };
    }
}
