using System.Drawing;
using XIAOFUTools.Features.Conversion.PolygonToDwgWithFill.Core;
using XIAOFUTools.Features.Conversion.PolygonToDwgWithFill.Infrastructure;
using XIAOFUTools.Shared.IO.Cad;

namespace XIAOFUTools.Tests.PolygonToDwgWithFill;

public sealed class DwgFillDocumentWriterTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Writer_CreatesDwgWithBoundaryAndSolidHatch()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-dwg-writer-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(root, "filled.dwg");
        var writer = new DwgFillDocumentWriter();
        var progress = new List<DwgFillExportProgress>();
        var polygons = new[]
        {
            new CadPolygonSnapshot
            {
                Color = Color.FromArgb(255, 36, 140, 210),
                LayerName = "测试填充",
                Rings = new List<List<CadCoordinate>>
                {
                    new()
                    {
                        new CadCoordinate(0, 0),
                        new CadCoordinate(10, 0),
                        new CadCoordinate(10, 10),
                        new CadCoordinate(0, 10),
                        new CadCoordinate(0, 0)
                    }
                }
            }
        };

        try
        {
            var result = await writer.WriteAsync(
                polygons,
                outputPath,
                new DwgFillExportOptions(
                    DwgExportVersion.AutoCad2018,
                    ExportBoundary: true,
                    ExportHatch: true,
                    LineWidthMillimeters: 0.25,
                    HatchTransparency: 30),
                () => false,
                progress.Add);

            Assert.False(result.Cancelled);
            Assert.Equal(1, result.ProcessedCount);
            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
            Assert.Contains(progress, item => item.ProcessedCount == 1 && item.TotalCount == 1);
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
