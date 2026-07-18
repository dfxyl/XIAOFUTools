using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.ExportShpFieldTable.Core;

namespace XIAOFUTools.Features.DataManagement.ExportShpFieldTable.Infrastructure
{
    internal sealed class ShpSchemaExcelExporter : IShpSchemaExcelExporter
    {
        public Task ExportAsync(
            IReadOnlyList<ShpLayerSchemaInfo> layers,
            string outputPath,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(layers);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    Export(layers, outputPath, cancellationToken);
                    completion.SetResult();
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
                Name = "XIAOFUTools-ShpSchemaExcelExport"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static void Export(
            IReadOnlyList<ShpLayerSchemaInfo> layers,
            string outputPath,
            CancellationToken cancellationToken)
        {
            const int xlOpenXmlWorkbook = 51;
            const int xlWorkbookNormal = -4143;
            dynamic? excel = null;
            dynamic? workbook = null;
            var worksheets = new List<object>();
            try
            {
                var excelType = Type.GetTypeFromProgID("Excel.Application") ??
                    throw new InvalidOperationException("未找到Excel组件，请确认已安装Office。");
                excel = Activator.CreateInstance(excelType);
                excel.Visible = false;
                excel.DisplayAlerts = false;
                workbook = excel.Workbooks.Add();
                RemoveDefaultSheets(workbook);

                dynamic? summarySheet = null;
                dynamic? summarySheets = null;
                try
                {
                    summarySheets = workbook.Sheets;
                    summarySheet = summarySheets[1];
                    summarySheet.Name = "图层";
                    WriteLayerSummary(summarySheet, layers);
                    worksheets.Add(summarySheet);
                    summarySheet = null;
                }
                finally
                {
                    Release(summarySheet);
                    Release(summarySheets);
                }

                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "图层" };
                foreach (var layer in layers)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    dynamic? sheets = null;
                    dynamic? after = null;
                    dynamic? fieldSheet = null;
                    try
                    {
                        sheets = workbook.Sheets;
                        after = sheets[sheets.Count];
                        fieldSheet = sheets.Add(After: after);
                        var sheetName = BuildUniqueSheetName(SanitizeSheetName(layer.Name), usedNames);
                        fieldSheet.Name = sheetName;
                        WriteFieldSheet(fieldSheet, layer);
                        worksheets.Add(fieldSheet);
                        fieldSheet = null;
                        usedNames.Add(sheetName);
                    }
                    finally
                    {
                        Release(fieldSheet);
                        Release(after);
                        Release(sheets);
                    }
                }

                workbook.SaveAs(
                    outputPath,
                    outputPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                        ? xlOpenXmlWorkbook
                        : xlWorkbookNormal);
            }
            finally
            {
                foreach (var sheet in worksheets)
                {
                    Release(sheet);
                }

                if (workbook != null)
                {
                    try { workbook.Close(false); } catch (COMException) { }
                    Release(workbook);
                }

                if (excel != null)
                {
                    try { excel.Quit(); } catch (COMException) { }
                    Release(excel);
                }
            }
        }

        private static void RemoveDefaultSheets(dynamic workbook)
        {
            dynamic? sheets = null;
            try
            {
                sheets = workbook.Sheets;
                while (sheets.Count > 1)
                {
                    dynamic? sheet = null;
                    try
                    {
                        sheet = sheets[sheets.Count];
                        sheet.Delete();
                    }
                    finally { Release(sheet); }
                }
            }
            finally { Release(sheets); }
        }

        private static void WriteLayerSummary(dynamic sheet, IReadOnlyList<ShpLayerSchemaInfo> layers)
        {
            var headers = new[] { "序号", "图层别名", "几何类型", "属性表名", "约束条件", "备注" };
            for (var index = 0; index < headers.Length; index++) sheet.Cells[1, index + 1] = headers[index];
            for (var index = 0; index < layers.Count; index++)
            {
                var row = index + 2;
                sheet.Cells[row, 1] = index + 1;
                sheet.Cells[row, 2] = string.IsNullOrWhiteSpace(layers[index].AliasName) ? layers[index].Name : layers[index].AliasName;
                sheet.Cells[row, 3] = layers[index].GeometryType;
                sheet.Cells[row, 4] = layers[index].Name;
                sheet.Cells[row, 5] = string.Empty;
                sheet.Cells[row, 6] = string.Empty;
            }
            FormatTable(sheet, layers.Count, headers.Length);
        }

        private static void WriteFieldSheet(dynamic sheet, ShpLayerSchemaInfo layer)
        {
            var headers = new[] { "序号", "字段别名", "字段代码", "字段类型", "字段长度", "小数位数", "值域", "约束条件" };
            for (var index = 0; index < headers.Length; index++) sheet.Cells[1, index + 1] = headers[index];
            for (var index = 0; index < layer.Fields.Count; index++)
            {
                var row = index + 2;
                var field = layer.Fields[index];
                sheet.Cells[row, 1] = index + 1;
                sheet.Cells[row, 2] = string.IsNullOrWhiteSpace(field.AliasName) ? field.FieldName : field.AliasName;
                sheet.Cells[row, 3] = field.FieldName;
                sheet.Cells[row, 4] = field.FieldType;
                sheet.Cells[row, 5] = field.Length?.ToString() ?? string.Empty;
                sheet.Cells[row, 6] = field.Scale?.ToString() ?? string.Empty;
                sheet.Cells[row, 7] = string.Empty;
                sheet.Cells[row, 8] = string.Empty;
            }
            FormatTable(sheet, layer.Fields.Count, headers.Length);
        }

        private static void FormatTable(dynamic sheet, int dataRowCount, int columnCount)
        {
            dynamic? header = null;
            try
            {
                header = sheet.Range[sheet.Cells[1, 1], sheet.Cells[1, columnCount]];
                header.Font.Bold = true;
                header.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                header.Borders.LineStyle = 1;
                header.Borders.Weight = 2;
            }
            finally { Release(header); }

            if (dataRowCount > 0)
            {
                dynamic? data = null;
                try
                {
                    data = sheet.Range[sheet.Cells[2, 1], sheet.Cells[dataRowCount + 1, columnCount]];
                    data.Borders.LineStyle = 1;
                    data.Borders.Weight = 2;
                }
                finally { Release(data); }
            }
            sheet.Columns.AutoFit();
        }

        private static string SanitizeSheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Sheet";
            foreach (var character in new[] { '\\', '/', '?', '*', '[', ']', ':' }) name = name.Replace(character, '_');
            return name.Length > 31 ? name[..31] : name;
        }

        private static string BuildUniqueSheetName(string name, ISet<string> usedNames)
        {
            var candidate = name;
            for (var index = 1; usedNames.Contains(candidate); index++)
            {
                var suffix = $"_{index}";
                candidate = name.Length + suffix.Length > 31
                    ? name[..Math.Max(1, 31 - suffix.Length)] + suffix
                    : name + suffix;
            }
            return candidate;
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
