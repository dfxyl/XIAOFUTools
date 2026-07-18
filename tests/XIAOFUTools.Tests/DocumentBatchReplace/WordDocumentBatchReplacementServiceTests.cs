using XIAOFUTools.Features.Conversion.DocumentBatchReplace;

namespace XIAOFUTools.Tests.DocumentBatchReplace;

public sealed class WordDocumentBatchReplacementServiceTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task UniqueOutputPath_PreservesNameAndAdvancesSuffix()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"xiaofutools-document-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var targetPath = Path.Combine(directory, "report.docx");
        await File.WriteAllTextAsync(targetPath, "original");
        await File.WriteAllTextAsync(Path.Combine(directory, "report_1.docx"), "copy");

        try
        {
            var actual = WordDocumentBatchReplacementService.GetUniqueOutputPath(targetPath);

            Assert.Equal(Path.Combine(directory, "report_2.docx"), actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
