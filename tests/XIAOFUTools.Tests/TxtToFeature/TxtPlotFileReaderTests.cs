using System.Text;
using XIAOFUTools.Features.Conversion.TxtToFeature.Infrastructure;

namespace XIAOFUTools.Tests.TxtToFeature;

public sealed class TxtPlotFileReaderTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Reader_DetectsUtf8BomAndReturnsParsedPlots()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"xiaofutools-txt-{Guid.NewGuid():N}.txt");
        var content = string.Join(Environment.NewLine, new[]
        {
            "[地块坐标]",
            "DK001,@",
            "P1,1,30.0,120.0",
            "P2,1,30.0,121.0",
            "P3,1,31.0,121.0"
        });
        await File.WriteAllTextAsync(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var reader = new TxtPlotFileReader();

        try
        {
            var result = await reader.ReadAsync(filePath, "编号,@", swapXy: false, CancellationToken.None);

            Assert.Contains("UTF-8 BOM", result.EncodingDescription, StringComparison.Ordinal);
            Assert.Equal(5, result.LineCount);
            var plot = Assert.Single(result.Plots);
            Assert.Equal("DK001", plot.Attributes["编号"]);
            Assert.Equal(120.0, plot.Rings.Single().Points[0].X);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
