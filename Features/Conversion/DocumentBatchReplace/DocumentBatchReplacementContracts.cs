using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.Conversion.DocumentBatchReplace
{
    internal sealed record DocumentReplacementRule(string FindText, string ReplaceText);

    internal sealed record DocumentBatchReplacementRequest(
        IReadOnlyList<string> SourceFiles,
        IReadOnlyList<DocumentReplacementRule> Rules,
        bool SaveAsCopy,
        string OutputFolder,
        bool MatchCase,
        bool MatchWholeWord,
        bool MatchByte,
        bool UseWildcards);

    internal sealed record DocumentBatchReplacementProgress(
        string Message,
        bool IsError,
        int Processed,
        int Total);

    internal sealed record DocumentInputResolution(
        IReadOnlyList<string> Files,
        int SkippedCount);

    internal interface IDocumentInputPathResolver
    {
        DocumentInputResolution Resolve(
            IEnumerable<string> paths,
            bool traverseSubfolders);
    }

    internal interface IDocumentBatchReplacementService
    {
        Task ReplaceAsync(
            DocumentBatchReplacementRequest request,
            IProgress<DocumentBatchReplacementProgress>? progress,
            CancellationToken cancellationToken);
    }
}
