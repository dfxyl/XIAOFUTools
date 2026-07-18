using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Word = Microsoft.Office.Interop.Word;
using XIAOFUTools.Shared.IO;

namespace XIAOFUTools.Features.Conversion.WordToPdf
{
    internal sealed record WordPdfConversionRequest(
        string InputFolder,
        string OutputFolder,
        bool SaveToSourcePath,
        bool KeepOriginalStructure,
        bool TraverseSubfolders);

    internal sealed record WordPdfConversionProgress(
        string Message,
        bool IsError,
        int Processed,
        int Total);

    internal interface IWordPdfConversionService
    {
        bool DirectoryExists(string path);

        Task<int> ConvertAsync(
            WordPdfConversionRequest request,
            IProgress<WordPdfConversionProgress>? progress,
            CancellationToken cancellationToken);
    }

    internal sealed class WordPdfConversionService : IWordPdfConversionService
    {
        private static readonly string[] SupportedExtensions = { ".docx", ".doc", ".docm" };

        public bool DirectoryExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

        public Task<int> ConvertAsync(
            WordPdfConversionRequest request,
            IProgress<WordPdfConversionProgress>? progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var completion = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    completion.SetResult(ConvertDocuments(request, progress, cancellationToken));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    completion.SetCanceled(cancellationToken);
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            })
            {
                IsBackground = true,
                Name = "XIAOFUTools-WordToPdf"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static int ConvertDocuments(
            WordPdfConversionRequest request,
            IProgress<WordPdfConversionProgress>? progress,
            CancellationToken cancellationToken)
        {
            var files = EnumerateFiles(request.InputFolder, request.TraverseSubfolders);
            if (files.Count == 0)
            {
                Report(progress, "未找到Word文件", false, 0, 0);
                return 0;
            }

            Report(progress, $"找到 {files.Count} 个Word文件，开始处理...", false, 0, files.Count);
            var outputOptions = new PdfOutputPathOptions(
                request.InputFolder,
                request.OutputFolder,
                request.SaveToSourcePath,
                request.KeepOriginalStructure);
            var processed = 0;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileName(file);
                Report(progress, $"正在处理: {name}", false, processed, files.Count);
                try
                {
                    ConvertOne(file, outputOptions, cancellationToken);
                    Report(progress, $"  - 完成: {name}", false, processed, files.Count);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Report(progress, $"处理文件 {name} 时出错: {exception.Message}", true, processed, files.Count);
                }

                processed++;
                Report(progress, string.Empty, false, processed, files.Count);
            }

            return files.Count;
        }

        private static void ConvertOne(
            string wordFilePath,
            PdfOutputPathOptions outputOptions,
            CancellationToken cancellationToken)
        {
            Word.Application? application = null;
            Word.Document? document = null;
            try
            {
                application = new Word.Application
                {
                    Visible = false,
                    DisplayAlerts = Word.WdAlertLevel.wdAlertsNone,
                    ScreenUpdating = false
                };
                document = application.Documents.Open(
                    FileName: wordFilePath,
                    ReadOnly: true,
                    AddToRecentFiles: false,
                    Visible: false);
                cancellationToken.ThrowIfCancellationRequested();
                var outputPath = PdfOutputPathPlanner.GetOutputPath(
                    wordFilePath,
                    Path.GetFileNameWithoutExtension(wordFilePath),
                    outputOptions);
                document.ExportAsFixedFormat(
                    outputPath,
                    Word.WdExportFormat.wdExportFormatPDF,
                    false,
                    Word.WdExportOptimizeFor.wdExportOptimizeForPrint,
                    Word.WdExportRange.wdExportAllDocument,
                    0,
                    0,
                    Word.WdExportItem.wdExportDocumentContent,
                    true,
                    true,
                    Word.WdExportCreateBookmarks.wdExportCreateHeadingBookmarks,
                    true,
                    true,
                    false);
            }
            finally
            {
                if (document != null)
                {
                    try { document.Close(false); } catch (COMException) { }
                    Release(document);
                }

                if (application != null)
                {
                    try { application.Quit(SaveChanges: false); } catch (COMException) { }
                    Release(application);
                }
            }
        }

        private static List<string> EnumerateFiles(string folder, bool recursive)
        {
            var files = new List<string>();
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var extension in SupportedExtensions)
            {
                try { files.AddRange(Directory.GetFiles(folder, $"*{extension}", searchOption)); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private static void Report(IProgress<WordPdfConversionProgress>? progress, string message, bool isError, int processed, int total) =>
            progress?.Report(new WordPdfConversionProgress(message, isError, processed, total));

        private static void Release(object value)
        {
            try { Marshal.ReleaseComObject(value); }
            catch (COMException) { }
            catch (InvalidComObjectException) { }
        }
    }
}
