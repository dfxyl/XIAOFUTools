using XIAOFUTools.Features.Conversion.ImagesToPdf.Infrastructure;

namespace XIAOFUTools.Tests.ImagesToPdf;

public sealed class ImagePdfFileStoreTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Store_FindsImagesGroupsFoldersAndPlansNonCollidingOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-images-pdf-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var nested = Path.Combine(input, "child");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(input, "B.JPG"), "image");
        File.WriteAllText(Path.Combine(nested, "a.png"), "image");
        var store = new ImagePdfFileStore();

        try
        {
            Assert.True(store.DirectoryExists(input));
            Assert.Single(store.GetImageFiles(input, includeSubfolders: false, new[] { ".jpg", ".png" }));
            Assert.Equal(2, store.GetImageFiles(input, includeSubfolders: true, new[] { ".jpg", ".png" }).Count);
            Assert.Equal(2, store.GetFolders(input, traverseSubfolders: true).Count);

            var request = new ImagePdfOutputPathRequest(
                nested,
                input,
                output,
                SaveToSourcePath: false,
                KeepOriginalStructure: true,
                PdfName: "child");
            var firstPath = store.CreateUniquePdfOutputPath(request);
            File.WriteAllText(firstPath, "pdf");
            var secondPath = store.CreateUniquePdfOutputPath(request);

            Assert.Equal(Path.Combine(output, "child", "child.pdf"), firstPath);
            Assert.Equal(Path.Combine(output, "child", "child_1.pdf"), secondPath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Writer_CreatesPdfFromValidImage()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-images-pdf-{Guid.NewGuid():N}");
        var imagePath = Path.Combine(root, "image.png");
        var outputPath = Path.Combine(root, "output", "image.pdf");
        var fileStore = new ImagePdfFileStore();
        var writer = new ImagePdfDocumentWriter(fileStore);
        Directory.CreateDirectory(root);
        File.WriteAllBytes(
            imagePath,
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL9WQAAAABJRU5ErkJggg=="));

        try
        {
            writer.Write(
                new[] { imagePath },
                outputPath,
                dpi: 300,
                CancellationToken.None,
                _ => { },
                _ => { });

            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
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
