using System;
using System.Data;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Features.Analysis.MultiOverlaySummary.Infrastructure
{
    internal sealed class MultiOverlayExcelExporter : IMultiOverlayExcelExporter
    {
        public Task ExportAsync(DataTable resultTable, int decimalPlaces, string filePath, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(resultTable);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { Export(resultTable, decimalPlaces, filePath, cancellationToken); completion.SetResult(); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { completion.SetCanceled(cancellationToken); }
                catch (Exception exception) { completion.SetException(exception); }
            }) { IsBackground = true, Name = "XIAOFUTools-MultiOverlayExcelExport" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static void Export(DataTable table, int decimalPlaces, string filePath, CancellationToken token)
        {
            Excel.Application? app = null;
            Excel.Workbooks? books = null;
            Excel.Workbook? book = null;
            Excel.Sheets? sheets = null;
            Excel.Worksheet? sheet = null;
            try
            {
                app = new Excel.Application { Visible = false, DisplayAlerts = false };
                books = app.Workbooks; book = books.Add(); sheets = book.Sheets;
                sheet = (Excel.Worksheet)sheets[1]; sheet.Name = "多图层压盖汇总表";
                for (var column = 0; column < table.Columns.Count; column++) SetCell(sheet, 1, column + 1, table.Columns[column].ColumnName, null);
                Excel.Range? header = null;
                try
                {
                    header = sheet.Range[sheet.Cells[1, 1], sheet.Cells[1, table.Columns.Count]];
                    header.Font.Bold = true; header.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(79, 129, 189));
                    header.Font.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.White); header.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                }
                finally { Release(header); }
                var format = decimalPlaces > 0 ? $"0.{new string('0', decimalPlaces)}" : "0";
                for (var row = 0; row < table.Rows.Count; row++)
                {
                    token.ThrowIfCancellationRequested();
                    for (var column = 0; column < table.Columns.Count; column++)
                    {
                        var value = table.Rows[row][column];
                        SetCell(sheet, row + 2, column + 1, value is double number ? Math.Round(number, decimalPlaces) : value?.ToString() ?? string.Empty, value is double ? format : null);
                    }
                }
                Excel.Range? total = null;
                try
                {
                    total = sheet.Range[sheet.Cells[table.Rows.Count + 1, 1], sheet.Cells[table.Rows.Count + 1, table.Columns.Count]];
                    total.Font.Bold = true; total.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(221, 235, 247));
                }
                finally { Release(total); }
                Excel.Range? columns = null; Excel.Range? data = null;
                try
                {
                    columns = sheet.Columns; columns.AutoFit();
                    data = sheet.Range[sheet.Cells[1, 1], sheet.Cells[table.Rows.Count + 1, table.Columns.Count]];
                    data.Borders.LineStyle = Excel.XlLineStyle.xlContinuous; data.Borders.Weight = Excel.XlBorderWeight.xlThin;
                }
                finally { Release(data); Release(columns); }
                book.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook);
            }
            finally
            {
                Release(sheet); Release(sheets);
                if (book != null) { try { book.Close(false); } catch (COMException) { } Release(book); }
                Release(books);
                if (app != null) { try { app.Quit(); } catch (COMException) { } Release(app); }
            }
        }

        private static void SetCell(Excel.Worksheet sheet, int row, int column, object value, string? numberFormat)
        {
            Excel.Range? cell = null;
            try { cell = (Excel.Range)sheet.Cells[row, column]; cell.Value2 = value; if (numberFormat != null) cell.NumberFormat = numberFormat; }
            finally { Release(cell); }
        }

        private static void Release(object? value)
        {
            if (value == null) return;
            try { if (Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
            catch (COMException) { }
            catch (InvalidComObjectException) { }
        }
    }
}
