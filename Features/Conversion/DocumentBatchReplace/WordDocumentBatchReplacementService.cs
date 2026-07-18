using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Word = Microsoft.Office.Interop.Word;

namespace XIAOFUTools.Features.Conversion.DocumentBatchReplace
{
    internal sealed class WordDocumentBatchReplacementService
        : IDocumentBatchReplacementService
    {
        public Task ReplaceAsync(
            DocumentBatchReplacementRequest request,
            IProgress<DocumentBatchReplacementProgress>? progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.SaveAsCopy)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputFolder);
            }

            var taskSource = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var staThread = new Thread(() =>
            {
                try
                {
                    ReplaceDocuments(request, progress, cancellationToken);
                    taskSource.SetResult();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    taskSource.SetCanceled(cancellationToken);
                }
                catch (Exception exception)
                {
                    taskSource.SetException(exception);
                }
            })
            {
                IsBackground = true,
                Name = "XIAOFUTools-WordDocumentBatchReplace"
            };
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            return taskSource.Task;
        }

        internal static string GetUniqueOutputPath(string targetPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
            if (!File.Exists(targetPath))
            {
                return targetPath;
            }

            var directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(targetPath);
            var extension = Path.GetExtension(targetPath);
            var index = 1;
            string candidatePath;
            do
            {
                candidatePath = Path.Combine(directory, $"{fileName}_{index}{extension}");
                index++;
            }
            while (File.Exists(candidatePath));

            return candidatePath;
        }

        private static void ReplaceDocuments(
            DocumentBatchReplacementRequest request,
            IProgress<DocumentBatchReplacementProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (request.SaveAsCopy)
            {
                Directory.CreateDirectory(request.OutputFolder);
            }

            var total = request.SourceFiles.Count;
            var processed = 0;
            foreach (var sourceFile in request.SourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(sourceFile);
                Report(progress, $"正在处理: {fileName}", false, processed, total);

                try
                {
                    var targetFile = request.SaveAsCopy
                        ? CreateCopy(sourceFile, request.OutputFolder)
                        : sourceFile;
                    ReplaceSingleDocument(targetFile, request, cancellationToken);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Report(
                            progress,
                            $"  - 完成: {Path.GetFileName(targetFile)}",
                            false,
                            processed,
                            total);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (COMException exception)
                {
                    Report(
                        progress,
                        $"处理文件 {fileName} 时发生COM错误: {exception.Message}",
                        true,
                        processed,
                        total);
                }
                catch (Exception exception)
                {
                    Report(
                        progress,
                        $"处理文件 {fileName} 时出错: {exception.Message}",
                        true,
                        processed,
                        total);
                }

                processed++;
                Report(progress, string.Empty, false, processed, total);
            }
        }

        private static void ReplaceSingleDocument(
            string filePath,
            DocumentBatchReplacementRequest request,
            CancellationToken cancellationToken)
        {
            Word.Application? wordApplication = null;
            Word.Document? document = null;
            try
            {
                wordApplication = new Word.Application
                {
                    Visible = false,
                    DisplayAlerts = Word.WdAlertLevel.wdAlertsNone,
                    ScreenUpdating = false
                };
                document = wordApplication.Documents.Open(
                    FileName: filePath,
                    ReadOnly: false,
                    AddToRecentFiles: false,
                    Visible: false);

                foreach (var rule in request.Rules)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ApplyRule(document, rule, request);
                }

                cancellationToken.ThrowIfCancellationRequested();
                document.Save();
            }
            finally
            {
                ReleaseDocument(document);
                ReleaseApplication(wordApplication);
            }
        }

        private static void ApplyRule(
            Word.Document document,
            DocumentReplacementRule rule,
            DocumentBatchReplacementRequest request)
        {
            Word.Range? content = null;
            dynamic? find = null;
            dynamic? replacement = null;
            try
            {
                content = document.Content;
                find = content.Find;
                replacement = find.Replacement;
                find.ClearFormatting();
                replacement.ClearFormatting();
                find.Text = rule.FindText;
                replacement.Text = rule.ReplaceText;
                find.Forward = true;
                find.Wrap = Word.WdFindWrap.wdFindContinue;
                find.Format = false;
                find.MatchCase = request.MatchCase;
                find.MatchWholeWord = request.MatchWholeWord;
                find.MatchWildcards = request.UseWildcards;
                find.MatchSoundsLike = false;
                find.MatchAllWordForms = false;
                try
                {
                    find.MatchByte = request.MatchByte;
                }
                catch (COMException)
                {
                }

                find.Execute(Replace: Word.WdReplace.wdReplaceAll);
            }
            finally
            {
                ReleaseComObjectSafely(replacement);
                ReleaseComObjectSafely(find);
                ReleaseComObjectSafely(content);
            }
        }

        private static string CreateCopy(string sourceFile, string outputFolder)
        {
            var outputPath = GetUniqueOutputPath(Path.Combine(
                outputFolder,
                Path.GetFileName(sourceFile)));
            File.Copy(sourceFile, outputPath, overwrite: false);
            return outputPath;
        }

        private static void ReleaseDocument(Word.Document? document)
        {
            if (document == null)
            {
                return;
            }

            try
            {
                document.Close(SaveChanges: false);
            }
            catch (COMException)
            {
            }
            finally
            {
                ReleaseComObjectSafely(document);
            }
        }

        private static void ReleaseApplication(Word.Application? application)
        {
            if (application == null)
            {
                return;
            }

            try
            {
                application.Quit(SaveChanges: false);
            }
            catch (COMException)
            {
            }
            finally
            {
                ReleaseComObjectSafely(application);
            }
        }

        private static void ReleaseComObjectSafely(object? value)
        {
            if (value == null || !Marshal.IsComObject(value))
            {
                return;
            }

            try
            {
                Marshal.ReleaseComObject(value);
            }
            catch (COMException)
            {
            }
            catch (InvalidComObjectException)
            {
            }
        }

        private static void Report(
            IProgress<DocumentBatchReplacementProgress>? progress,
            string message,
            bool isError,
            int processed,
            int total)
        {
            progress?.Report(new DocumentBatchReplacementProgress(
                message,
                isError,
                processed,
                total));
        }
    }
}
