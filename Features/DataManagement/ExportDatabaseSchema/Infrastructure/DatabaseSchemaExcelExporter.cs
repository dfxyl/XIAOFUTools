using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Excel = Microsoft.Office.Interop.Excel;
using XIAOFUTools.Features.DataManagement.ExportDatabaseSchema.Core;

namespace XIAOFUTools.Features.DataManagement.ExportDatabaseSchema.Infrastructure
{
    internal sealed class DatabaseSchemaExcelExporter : IDatabaseSchemaExcelExporter
    {
        public Task ExportAsync(
            IReadOnlyList<FeatureDatasetInfo> featureDatasets,
            IReadOnlyList<FeatureClassInfo> featureClasses,
            string outputPath,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(featureDatasets);
            ArgumentNullException.ThrowIfNull(featureClasses);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { Export(featureDatasets, featureClasses, outputPath, cancellationToken); completion.SetResult(); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { completion.SetCanceled(cancellationToken); }
                catch (Exception exception) { completion.SetException(exception); }
            }) { IsBackground = true, Name = "XIAOFUTools-DatabaseSchemaExcelExport" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static void Export(
            IReadOnlyList<FeatureDatasetInfo> datasets,
            IReadOnlyList<FeatureClassInfo> classes,
            string outputPath,
            CancellationToken cancellationToken)
        {
            Excel.Application? application = null;
            Excel.Workbook? workbook = null;
            var retainedSheets = new List<Excel.Worksheet>();
            try
            {
                application = new Excel.Application { Visible = false, DisplayAlerts = false };
                workbook = application.Workbooks.Add();
                RemoveDefaultSheets(workbook);
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (datasets.Count > 0)
                {
                    var datasetSheet = GetFirstSheet(workbook);
                    datasetSheet.Name = "要素集";
                    WriteDatasets(datasetSheet, datasets);
                    retainedSheets.Add(datasetSheet);
                    usedNames.Add(datasetSheet.Name);
                    var summarySheet = AddSheet(workbook);
                    summarySheet.Name = "图层";
                    WriteSummary(summarySheet, classes);
                    retainedSheets.Add(summarySheet);
                    usedNames.Add(summarySheet.Name);
                }
                else
                {
                    var summarySheet = GetFirstSheet(workbook);
                    summarySheet.Name = "图层";
                    WriteSummary(summarySheet, classes);
                    retainedSheets.Add(summarySheet);
                    usedNames.Add(summarySheet.Name);
                }

                foreach (var featureClass in classes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (featureClass.Fields.Count == 0) continue;
                    var sheet = AddSheet(workbook);
                    sheet.Name = BuildUniqueSheetName(SanitizeSheetName(featureClass.Name), usedNames);
                    WriteFields(sheet, featureClass);
                    retainedSheets.Add(sheet);
                    usedNames.Add(sheet.Name);
                }

                workbook.SaveAs(outputPath, outputPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                    ? Excel.XlFileFormat.xlOpenXMLWorkbook
                    : Excel.XlFileFormat.xlWorkbookNormal);
            }
            finally
            {
                foreach (var sheet in retainedSheets) Release(sheet);
                if (workbook != null) { try { workbook.Close(false); } catch (COMException) { } Release(workbook); }
                if (application != null) { try { application.Quit(); } catch (COMException) { } Release(application); }
            }
        }

        private static void RemoveDefaultSheets(Excel.Workbook workbook)
        {
            Excel.Sheets? sheets = null;
            try
            {
                sheets = workbook.Sheets;
                while (sheets.Count > 1)
                {
                    Excel.Worksheet? sheet = null;
                    try { sheet = (Excel.Worksheet)sheets[sheets.Count]; sheet.Delete(); }
                    finally { Release(sheet); }
                }
            }
            finally { Release(sheets); }
        }

        private static Excel.Worksheet GetFirstSheet(Excel.Workbook workbook)
        {
            Excel.Sheets? sheets = null;
            try { sheets = workbook.Sheets; return (Excel.Worksheet)sheets[1]; }
            finally { Release(sheets); }
        }

        private static Excel.Worksheet AddSheet(Excel.Workbook workbook)
        {
            Excel.Sheets? sheets = null;
            Excel.Worksheet? after = null;
            try
            {
                sheets = workbook.Sheets;
                after = (Excel.Worksheet)sheets[sheets.Count];
                return (Excel.Worksheet)sheets.Add(After: after);
            }
            finally { Release(after); Release(sheets); }
        }

        private static void WriteDatasets(Excel.Worksheet sheet, IReadOnlyList<FeatureDatasetInfo> datasets)
        {
            var headers = new[] { "序号", "要素集名称", "要素集别名", "备注" };
            for (var i = 0; i < headers.Length; i++) sheet.Cells[1, i + 1] = headers[i];
            for (var i = 0; i < datasets.Count; i++)
            {
                var row = i + 2; var item = datasets[i];
                sheet.Cells[row, 1] = item.Index; sheet.Cells[row, 2] = item.DatasetName;
                sheet.Cells[row, 3] = item.DatasetAlias ?? item.DatasetName; sheet.Cells[row, 4] = item.Notes;
            }
            FormatTable(sheet, datasets.Count, headers.Length);
        }

        private static void WriteSummary(Excel.Worksheet sheet, IReadOnlyList<FeatureClassInfo> classes)
        {
            var headers = new[] { "序号", "图层别名", "几何类型", "属性表名", "要素集", "字段数", "备注" };
            for (var i = 0; i < headers.Length; i++) sheet.Cells[1, i + 1] = headers[i];
            for (var i = 0; i < classes.Count; i++)
            {
                var row = i + 2; var item = classes[i];
                sheet.Cells[row, 1] = i + 1; sheet.Cells[row, 2] = item.AliasName ?? item.Name;
                sheet.Cells[row, 3] = item.GeometryType; sheet.Cells[row, 4] = item.Name;
                sheet.Cells[row, 5] = item.FeatureDataset; sheet.Cells[row, 6] = item.Fields.Count; sheet.Cells[row, 7] = string.Empty;
            }
            FormatTable(sheet, classes.Count, headers.Length);
        }

        private static void WriteFields(Excel.Worksheet sheet, FeatureClassInfo featureClass)
        {
            var headers = new[] { "序号", "字段别名", "字段代码", "字段类型", "字段长度", "精度", "小数位数", "允许空值", "默认值", "约束", "备注" };
            for (var i = 0; i < headers.Length; i++) sheet.Cells[1, i + 1] = headers[i];
            for (var i = 0; i < featureClass.Fields.Count; i++)
            {
                var row = i + 2; var item = featureClass.Fields[i];
                sheet.Cells[row, 1] = i + 1; sheet.Cells[row, 2] = item.AliasName ?? item.FieldName;
                sheet.Cells[row, 3] = item.FieldName; sheet.Cells[row, 4] = item.FieldType;
                sheet.Cells[row, 5] = item.Length?.ToString() ?? string.Empty; sheet.Cells[row, 6] = item.Precision?.ToString() ?? string.Empty;
                sheet.Cells[row, 7] = item.Scale?.ToString() ?? string.Empty; sheet.Cells[row, 8] = item.IsNullable ? "是" : "否";
                sheet.Cells[row, 9] = item.DefaultValue; sheet.Cells[row, 10] = string.Empty; sheet.Cells[row, 11] = string.Empty;
            }
            FormatTable(sheet, featureClass.Fields.Count, headers.Length);
        }

        private static void FormatTable(Excel.Worksheet sheet, int dataRows, int columnCount)
        {
            Excel.Range? header = null;
            try
            {
                header = sheet.Range[sheet.Cells[1, 1], sheet.Cells[1, columnCount]];
                header.Font.Bold = true; header.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                header.Borders.LineStyle = Excel.XlLineStyle.xlContinuous; header.Borders.Weight = Excel.XlBorderWeight.xlThin;
            }
            finally { Release(header); }
            if (dataRows > 0)
            {
                Excel.Range? data = null;
                try
                {
                    data = sheet.Range[sheet.Cells[2, 1], sheet.Cells[dataRows + 1, columnCount]];
                    data.Borders.LineStyle = Excel.XlLineStyle.xlContinuous; data.Borders.Weight = Excel.XlBorderWeight.xlThin;
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
                candidate = name.Length + suffix.Length > 31 ? name[..Math.Max(1, 31 - suffix.Length)] + suffix : name + suffix;
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
