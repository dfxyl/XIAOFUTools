using System.Text;
using XIAOFUTools.Features.Conversion.FeatureToTxt.Core;

namespace XIAOFUTools.Tests.FeatureToTxt;

public sealed class FeatureCoordinateTextWriterTests
{
    [Fact]
    public void Write_EmitsClosingPointWithRingStartNumber()
    {
        var rings = CreateSquare();
        var content = new StringBuilder();
        var fields = new List<string> { string.Empty, "100", "DK001" };
        var configuration = new FeatureToTxtExportConfiguration(
            "J",
            2,
            true,
            false,
            false,
            false);

        new FeatureCoordinateTextFormatter().Write(rings, content, fields, configuration);

        var lines = content.ToString().Split(
            new[] { "\r\n", "\n" },
            StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("5,100,DK001,@", lines[0]);
        Assert.Equal("J1,1,20.00,100.00", lines[1]);
        Assert.Equal("J1,1,20.00,100.00", lines[^1]);
    }

    [Fact]
    public void BuildNumberedPoints_OmitsClosingPointWhenDisabled()
    {
        var configuration = new FeatureToTxtExportConfiguration(
            "P",
            3,
            false,
            false,
            false,
            true);

        var points = new FeatureCoordinateTextFormatter().BuildNumberedPoints(
            CreateSquare(),
            configuration);

        Assert.Equal(4, points.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, points.Select(point => point.PointIndex));
        Assert.DoesNotContain(points, point => point.IsClosingPoint);
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
