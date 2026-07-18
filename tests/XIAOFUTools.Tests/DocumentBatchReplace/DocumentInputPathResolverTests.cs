using XIAOFUTools.Features.Conversion.DocumentBatchReplace;

namespace XIAOFUTools.Tests.DocumentBatchReplace;

public sealed class DocumentInputPathResolverTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Resolve_FiltersSupportedDocumentsAndHonorsRecursion()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"xiaofutools-document-input-{Guid.NewGuid():N}");
        var nestedDirectory = Path.Combine(directory, "nested");
        Directory.CreateDirectory(nestedDirectory);
        await File.WriteAllTextAsync(Path.Combine(directory, "a.docx"), "a");
        await File.WriteAllTextAsync(Path.Combine(directory, "b.DOC"), "b");
        await File.WriteAllTextAsync(Path.Combine(directory, "ignored.txt"), "ignored");
        await File.WriteAllTextAsync(Path.Combine(nestedDirectory, "c.docm"), "c");

        try
        {
            var resolver = new DocumentInputPathResolver();

            var topLevel = resolver.Resolve([directory], traverseSubfolders: false);
            var recursive = resolver.Resolve([directory], traverseSubfolders: true);

            Assert.Equal(2, topLevel.Files.Count);
            Assert.Equal(3, recursive.Files.Count);
            Assert.All(recursive.Files, file =>
                Assert.Contains(
                    Path.GetExtension(file),
                    DocumentInputPathResolver.SupportedExtensions));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Resolve_ReportsInvalidAndUnsupportedPaths()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"xiaofutools-document-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var unsupportedFile = Path.Combine(directory, "notes.txt");
        await File.WriteAllTextAsync(unsupportedFile, "notes");

        try
        {
            var result = new DocumentInputPathResolver().Resolve(
                [unsupportedFile, Path.Combine(directory, "missing.docx"), "   "],
                traverseSubfolders: false);

            Assert.Empty(result.Files);
            Assert.Equal(3, result.SkippedCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
