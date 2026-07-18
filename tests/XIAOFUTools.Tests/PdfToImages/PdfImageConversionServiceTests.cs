using PdfSharp.Pdf;
using XIAOFUTools.Features.Conversion.PdfToImages.Infrastructure;

namespace XIAOFUTools.Tests.PdfToImages;

public sealed class PdfImageConversionServiceTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Service_RendersPdfPageToConfiguredImageFormat()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-pdf-images-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        var pdfPath = Path.Combine(input, "document.pdf");
        Directory.CreateDirectory(input);
        using (var document = new PdfDocument())
        {
            document.AddPage();
            document.Save(pdfPath);
        }

        var service = new PdfImageConversionService();
        var messages = new List<PdfImageConversionProgress>();
        try
        {
            service.Convert(
                new PdfImageConversionRequest(
                    input,
                    output,
                    SaveToSourcePath: false,
                    KeepOriginalStructure: false,
                    CreateSeparateFolder: true,
                    TraverseSubfolders: false,
                    Resolution: 72,
                    OutputFormat: "PNG"),
                CancellationToken.None,
                messages.Add);

            Assert.True(File.Exists(Path.Combine(output, "document", "document_page001.png")));
            Assert.Contains(messages, progress => progress.ProcessedFiles == 1 && progress.TotalFiles == 1);
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
