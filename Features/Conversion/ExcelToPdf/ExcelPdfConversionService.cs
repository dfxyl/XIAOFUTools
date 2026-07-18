using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Excel = Microsoft.Office.Interop.Excel;
using XIAOFUTools.Shared.IO;

namespace XIAOFUTools.Features.Conversion.ExcelToPdf
{
    internal sealed record ExcelPdfConversionRequest(
        string InputFolder,
        string OutputFolder,
        bool SaveToSourcePath,
        bool KeepOriginalStructure,
        bool SeparateWorksheets,
        bool TraverseSubfolders,
        int PageOrientation,
        int PageSize,
        int PrintLayout);

    internal sealed record ExcelPdfConversionProgress(
        string Message,
        bool IsError,
        int Processed,
        int Total);

    internal interface IExcelPdfConversionService
    {
        bool DirectoryExists(string path);

        Task<int> ConvertAsync(
            ExcelPdfConversionRequest request,
            IProgress<ExcelPdfConversionProgress>? progress,
            CancellationToken cancellationToken);
    }

    internal sealed class ExcelPdfConversionService : IExcelPdfConversionService
    {
        private static readonly string[] SupportedExtensions = { ".xlsx", ".xls", ".xlsm" };

        public bool DirectoryExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

        public Task<int> ConvertAsync(
            ExcelPdfConversionRequest request,
            IProgress<ExcelPdfConversionProgress>? progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var completion = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    completion.SetResult(ConvertWorkbooks(request, progress, cancellationToken));
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
                Name = "XIAOFUTools-ExcelToPdf"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static int ConvertWorkbooks(
            ExcelPdfConversionRequest request,
            IProgress<ExcelPdfConversionProgress>? progress,
            CancellationToken cancellationToken)
        {
            var files = EnumerateFiles(request.InputFolder, request.TraverseSubfolders);
            if (files.Count == 0)
            {
                Report(progress, "未找到Excel文件", false, 0, 0);
                return 0;
            }

            Report(progress, $"找到 {files.Count} 个Excel文件，开始处理...", false, 0, files.Count);
            var outputOptions = new PdfOutputPathOptions(
                request.InputFolder,
                request.OutputFolder,
                request.SaveToSourcePath,
                request.KeepOriginalStructure);
            Excel.Application? application = null;
            try
            {
                application = new Excel.Application
                {
                    Visible = false,
                    DisplayAlerts = false,
                    ScreenUpdating = false
                };

                var processed = 0;
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(file);
                    Report(progress, $"正在处理: {name}", false, processed, files.Count);
                    try
                    {
                        ConvertOne(application, file, request, outputOptions, progress, processed, files.Count, cancellationToken);
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
            }
            finally
            {
                if (application != null)
                {
                    try { application.Quit(); } catch (COMException) { }
                    Release(application);
                }
            }

            return files.Count;
        }

        private static void ConvertOne(
            Excel.Application application,
            string sourceFile,
            ExcelPdfConversionRequest request,
            PdfOutputPathOptions outputOptions,
            IProgress<ExcelPdfConversionProgress>? progress,
            int processed,
            int total,
            CancellationToken cancellationToken)
        {
            Excel.Workbooks? workbooks = null;
            Excel.Workbook? workbook = null;
            try
            {
                workbooks = application.Workbooks;
                workbook = workbooks.Open(sourceFile);
                ApplyPageSettings(workbook, request);
                cancellationToken.ThrowIfCancellationRequested();

                if (request.SeparateWorksheets)
                {
                    ExportWorksheets(workbook, sourceFile, outputOptions, progress, processed, total, cancellationToken);
                }
                else
                {
                    var outputPath = PdfOutputPathPlanner.GetOutputPath(
                        sourceFile,
                        Path.GetFileNameWithoutExtension(sourceFile),
                        outputOptions);
                    workbook.ExportAsFixedFormat(
                        Excel.XlFixedFormatType.xlTypePDF,
                        outputPath,
                        Excel.XlFixedFormatQuality.xlQualityStandard,
                        true,
                        false);
                }
            }
            finally
            {
                if (workbook != null)
                {
                    try { workbook.Close(false); } catch (COMException) { }
                    Release(workbook);
                }

                if (workbooks != null)
                {
                    Release(workbooks);
                }
            }
        }

        private static void ExportWorksheets(
            Excel.Workbook workbook,
            string sourceFile,
            PdfOutputPathOptions outputOptions,
            IProgress<ExcelPdfConversionProgress>? progress,
            int processed,
            int total,
            CancellationToken cancellationToken)
        {
            Excel.Sheets? sheets = null;
            try
            {
                sheets = workbook.Worksheets;
                for (var index = 1; index <= sheets.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Excel.Worksheet? sheet = null;
                    try
                    {
                        sheet = (Excel.Worksheet)sheets[index];
                        var outputPath = PdfOutputPathPlanner.GetOutputPath(
                            sourceFile,
                            $"{Path.GetFileNameWithoutExtension(sourceFile)}_{sheet.Name}",
                            outputOptions);
                        sheet.ExportAsFixedFormat(
                            Excel.XlFixedFormatType.xlTypePDF,
                            outputPath,
                            Excel.XlFixedFormatQuality.xlQualityStandard,
                            true,
                            false);
                        Report(progress, $"    - 工作表 '{sheet.Name}' 已导出", false, processed, total);
                    }
                    catch (COMException exception)
                    {
                        Report(progress, $"    - 导出工作表失败: {exception.Message}", true, processed, total);
                    }
                    finally
                    {
                        if (sheet != null)
                        {
                            Release(sheet);
                        }
                    }
                }
            }
            finally
            {
                if (sheets != null)
                {
                    Release(sheets);
                }
            }
        }

        private static void ApplyPageSettings(Excel.Workbook workbook, ExcelPdfConversionRequest request)
        {
            Excel.Sheets? sheets = null;
            try
            {
                sheets = workbook.Worksheets;
                for (var index = 1; index <= sheets.Count; index++)
                {
                    Excel.Worksheet? sheet = null;
                    try
                    {
                        sheet = (Excel.Worksheet)sheets[index];
                        var pageSetup = sheet.PageSetup;
                        try
                        {
                            if (request.PageOrientation == 1) pageSetup.Orientation = Excel.XlPageOrientation.xlLandscape;
                            else if (request.PageOrientation == 2) pageSetup.Orientation = Excel.XlPageOrientation.xlPortrait;
                            if (request.PageSize == 1) pageSetup.PaperSize = Excel.XlPaperSize.xlPaperA4;
                            else if (request.PageSize == 2) pageSetup.PaperSize = Excel.XlPaperSize.xlPaperA3;
                            if (request.PrintLayout == 1) pageSetup.Zoom = false;
                            else if (request.PrintLayout == 2)
                            {
                                pageSetup.Zoom = false; pageSetup.FitToPagesWide = 1; pageSetup.FitToPagesTall = 1;
                            }
                            else if (request.PrintLayout == 3)
                            {
                                pageSetup.Zoom = false; pageSetup.FitToPagesWide = 1; pageSetup.FitToPagesTall = false;
                            }
                            else if (request.PrintLayout == 4)
                            {
                                pageSetup.Zoom = false; pageSetup.FitToPagesWide = false; pageSetup.FitToPagesTall = 1;
                            }
                        }
                        finally { Release(pageSetup); }
                    }
                    catch (COMException) { }
                    finally { if (sheet != null) Release(sheet); }
                }
            }
            finally { if (sheets != null) Release(sheets); }
        }

        private static List<string> EnumerateFiles(string folder, bool recursive)
        {
            var files = new List<string>();
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var extension in SupportedExtensions)
            {
                try { files.AddRange(Directory.GetFiles(folder, $"*{extension}", option)); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private static void Report(IProgress<ExcelPdfConversionProgress>? progress, string message, bool isError, int processed, int total) =>
            progress?.Report(new ExcelPdfConversionProgress(message, isError, processed, total));

        private static void Release(object value)
        {
            try { Marshal.ReleaseComObject(value); }
            catch (COMException) { }
            catch (InvalidComObjectException) { }
        }
    }
}
