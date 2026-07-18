using System.Drawing;
using XIAOFUTools.Features.Conversion.PolygonToDxfWithFill.Core;
using XIAOFUTools.Features.Conversion.PolygonToDxfWithFill.Infrastructure;
using XIAOFUTools.Shared.IO.Cad;

namespace XIAOFUTools.Tests.PolygonToDxfWithFill;

public sealed class DxfFillDocumentWriterTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Writer_CreatesDxfWithBoundaryAndSolidHatch()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-dxf-writer-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(root, "filled.dxf");
        var writer = new DxfFillDocumentWriter();
        var progress = new List<DxfFillExportProgress>();
        var polygons = new[]
        {
            new CadPolygonSnapshot
            {
                Color = Color.FromArgb(255, 30, 120, 200),
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
                new DxfFillExportOptions(
                    DxfExportVersion.AutoCad2018,
                    ExportBoundary: true,
                    ExportHatch: true,
                    LineWidthMillimeters: 0.25,
                    HatchTransparency: 30),
                () => false,
                progress.Add);

            Assert.False(result.Cancelled);
            Assert.Equal(1, result.ProcessedCount);
            Assert.True(File.Exists(outputPath));
            Assert.NotEmpty(await File.ReadAllTextAsync(outputPath));
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
