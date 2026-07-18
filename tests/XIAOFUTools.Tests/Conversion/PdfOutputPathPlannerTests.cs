using XIAOFUTools.Shared.IO;

namespace XIAOFUTools.Tests.Conversion;

public sealed class PdfOutputPathPlannerTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOutputPath_PreservesStructureAndAvoidsExistingName()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-pdf-output-{Guid.NewGuid():N}");
        var inputRoot = Path.Combine(root, "input");
        var sourceDirectory = Path.Combine(inputRoot, "nested");
        var outputRoot = Path.Combine(root, "output");
        var sourceFile = Path.Combine(sourceDirectory, "report.docx");
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(sourceFile, "source");
        Directory.CreateDirectory(Path.Combine(outputRoot, "nested"));
        await File.WriteAllTextAsync(
            Path.Combine(outputRoot, "nested", "report.pdf"),
            "existing");

        try
        {
            var output = PdfOutputPathPlanner.GetOutputPath(
                sourceFile,
                "report",
                new PdfOutputPathOptions(
                    inputRoot,
                    outputRoot,
                    SaveToSourcePath: false,
                    KeepOriginalStructure: true));

            Assert.Equal(Path.Combine(outputRoot, "nested", "report_1.pdf"), output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOutputPath_UsesSourceFolderWhenConfigured()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-pdf-output-{Guid.NewGuid():N}");
        var sourceFile = Path.Combine(root, "book.xlsx");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(sourceFile, "source");

        try
        {
            var output = PdfOutputPathPlanner.GetOutputPath(
                sourceFile,
                "book",
                new PdfOutputPathOptions(
                    root,
                    string.Empty,
                    SaveToSourcePath: true,
                    KeepOriginalStructure: false));

            Assert.Equal(Path.Combine(root, "book.pdf"), output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
