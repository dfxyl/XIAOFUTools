using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.Analysis.LandClassTable.Core;
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Features.Analysis.LandClassTable.Infrastructure
{
    internal static partial class LandClassExcelExporter
    {
        internal static Task ExportResultsAsync(
            IReadOnlyList<LandClassProjectArea> projectAreas,
            IReadOnlyList<LandClassIntersectionArea> intersectionAreas,
            LandClassExportOptions options,
            Func<bool> isCancellationRequested,
            Action<int> reportProgress)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    ExportResults(projectAreas, intersectionAreas, options, isCancellationRequested, reportProgress);
                    completion.SetResult();
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }) { IsBackground = true, Name = "XIAOFUTools-LandClassExcelExport" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        internal static void ExportResults(
            IReadOnlyList<LandClassProjectArea> projectAreas,
            IReadOnlyList<LandClassIntersectionArea> intersectionAreas,
            LandClassExportOptions options,
            Func<bool> isCancelRequested,
            Action<int> reportProgress)
        {
            Directory.CreateDirectory(options.OutputFolder);
            ThrowIfCancelled(isCancelRequested);

            if (options.UseGroupedOutput)
            {
                var groupedProjectAreas = projectAreas
                    .GroupBy(x => NormalizeGroupValue(x.GroupValue), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                int totalGroups = Math.Max(1, groupedProjectAreas.Count);
                for (int i = 0; i < groupedProjectAreas.Count; i++)
                {
                    ThrowIfCancelled(isCancelRequested);
                    var group = groupedProjectAreas[i];
                    string groupValue = group.Key;
                    var groupedAreas = group.ToList();
                    var groupedIntersections = intersectionAreas
                        .Where(x => NormalizeGroupValue(x.GroupValue).Equals(groupValue, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var groupedResult = LandClassTableBuilder.BuildTable(groupedAreas, groupedIntersections, options.DecimalPlaces);
                    string fileName = $"地类表_{NormalizeOutputPart(group.Key)}.xlsx";
                    string outputPath = EnsureUniqueOutputPath(Path.Combine(options.OutputFolder, fileName));
                    ExportToExcel(groupedResult, outputPath, options);
                    reportProgress(CalculateExportProgress(i + 1, totalGroups));
                }

                return;
            }

            ThrowIfCancelled(isCancelRequested);
            var result = LandClassTableBuilder.BuildTable(projectAreas, intersectionAreas, options.DecimalPlaces);
            string singlePath = EnsureUniqueOutputPath(Path.Combine(options.OutputFolder, "地类表.xlsx"));
            ExportToExcel(result, singlePath, options);
            reportProgress(100);
        }


        private static int CalculateExportProgress(int completed, int total)
        {
            return 70 + (int)Math.Round(Math.Max(0, Math.Min(completed, total)) * 30.0 / Math.Max(1, total));
        }


        private static void ExportToExcel(LandClassTableResult result, string outputPath, LandClassExportOptions options)
        {
            Excel.Application excelApp = null;
            Excel.Workbooks workbooks = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet sheet = null;

            try
            {
                excelApp = new Excel.Application { Visible = false, DisplayAlerts = false };
                workbooks = excelApp.Workbooks;
                workbook = workbooks.Add();
                sheet = (Excel.Worksheet)workbook.Worksheets[1];
                sheet.Name = "Sheet1";

                WriteSheet(sheet, result, options);
                workbook.SaveAs(outputPath, Excel.XlFileFormat.xlOpenXMLWorkbook);
            }
            finally
            {
                try { if (workbook != null) { workbook.Close(false); Marshal.ReleaseComObject(workbook); } } catch { }
                try { if (workbooks != null) Marshal.ReleaseComObject(workbooks); } catch { }
                try { if (sheet != null) Marshal.ReleaseComObject(sheet); } catch { }
                try { if (excelApp != null) { excelApp.Quit(); Marshal.ReleaseComObject(excelApp); } } catch { }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }


        private static void WriteSheet(Excel.Worksheet sheet, LandClassTableResult result, LandClassExportOptions options)
        {
            var columns = result.Columns.ToList();
            int firstColumn = 2;
            bool includePlotName = options.IncludePlotName;
            int serialColumn = firstColumn;
            int plotColumn = includePlotName ? serialColumn + 1 : -1;
            int unitColumn = includePlotName ? firstColumn + 2 : firstColumn + 1;
            int ownerColumn = unitColumn + 1;
            int totalColumn = ownerColumn + 1;
            int classStartColumn = totalColumn + 1;
            int totalColumns = 4 + columns.Count + (includePlotName ? 1 : 0);
            int lastColumn = firstColumn + totalColumns - 1;
            int firstDataRow = 8;
            int totalRow = firstDataRow + result.Rows.Count;
            int footerRow = totalRow + 1;

            ApplyBaseLayout(sheet, lastColumn, footerRow, includePlotName);

            Merge(sheet, 2, firstColumn, 3, lastColumn);
            sheet.Cells[2, firstColumn] = $"土 地 分 类 权 属 地 类 面 积 汇 总 表({options.ReportYear}年)";

            int locationColumn = Math.Max(firstColumn + 5, firstColumn + totalColumns / 2);
            int unitLabelColumn = Math.Max(lastColumn - 1, firstColumn);
            Merge(sheet, 4, firstColumn, 4, Math.Min(locationColumn - 1, lastColumn));
            sheet.Cells[4, firstColumn] = $"权利人：{options.RightHolderName}";
            if (locationColumn <= lastColumn)
            {
                Merge(sheet, 4, locationColumn, 4, Math.Max(locationColumn, unitLabelColumn - 1));
                sheet.Cells[4, locationColumn] = $"所在地：{options.LocationName}";
            }
            if (unitLabelColumn <= lastColumn)
            {
                Merge(sheet, 4, unitLabelColumn, 4, lastColumn);
                Excel.Range unitRange = null;
                try
                {
                    unitRange = sheet.Range[sheet.Cells[4, unitLabelColumn], sheet.Cells[4, lastColumn]];
                    unitRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignRight;
                    unitRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                }
                finally
                {
                    ReleaseComObject(unitRange);
                }

                sheet.Cells[4, unitLabelColumn] = $"单位:{options.AreaUnit}";
            }

            WriteFixedHeaders(sheet, serialColumn, includePlotName);
            WriteClassHeaders(sheet, columns, classStartColumn);

            for (int i = 0; i < result.Rows.Count; i++)
            {
                var row = result.Rows[i];
                int excelRow = firstDataRow + i;
                sheet.Cells[excelRow, serialColumn] = i + 1;
                if (includePlotName)
                {
                    sheet.Cells[excelRow, plotColumn] = row.PlotName;
                }

                sheet.Cells[excelRow, unitColumn] = row.OwnerUnitName;
                sheet.Cells[excelRow, ownerColumn] = row.OwnerNatureName;

                for (int j = 0; j < columns.Count; j++)
                {
                    int excelColumn = classStartColumn + j;
                    var column = columns[j];
                    if (CanWriteDirectValue(columns, column))
                    {
                        row.Values.TryGetValue(column.Key, out double value);
                        sheet.Cells[excelRow, excelColumn] = RoundArea(value, options.DecimalPlaces);
                    }
                }

                WriteRowFormulas(sheet, columns, excelRow, totalColumn, classStartColumn);
            }

            if (includePlotName)
            {
                MergePlotNameCells(sheet, result.Rows, firstDataRow, plotColumn);
            }

            sheet.Cells[totalRow, serialColumn] = "总计";
            Merge(sheet, totalRow, serialColumn, totalRow, ownerColumn);
            sheet.Cells[totalRow, totalColumn] = $"=SUM({GetColumnName(totalColumn)}{firstDataRow}:{GetColumnName(totalColumn)}{totalRow - 1})";
            for (int j = 0; j < columns.Count; j++)
            {
                int excelColumn = classStartColumn + j;
                sheet.Cells[totalRow, excelColumn] = $"=SUM({GetColumnName(excelColumn)}{firstDataRow}:{GetColumnName(excelColumn)}{totalRow - 1})";
            }

            int reportCompanyEndColumn = Math.Min(firstColumn + 3, lastColumn);
            Merge(sheet, footerRow, firstColumn, footerRow, reportCompanyEndColumn);
            Excel.Range reportCompanyRange = null;
            try
            {
                reportCompanyRange = sheet.Range[sheet.Cells[footerRow, firstColumn], sheet.Cells[footerRow, reportCompanyEndColumn]];
                reportCompanyRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignLeft;
                reportCompanyRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
            }
            finally
            {
                ReleaseComObject(reportCompanyRange);
            }

            sheet.Cells[footerRow, firstColumn] = $"填报单位:{options.ReportCompany}";
            int preparedColumn = Math.Min(firstColumn + 4, lastColumn);
            int reviewedColumn = Math.Min(firstColumn + 7, lastColumn);
            int dateColumn = Math.Min(firstColumn + 10, lastColumn);
            if (preparedColumn > firstColumn)
            {
                Merge(sheet, footerRow, preparedColumn, footerRow, Math.Min(preparedColumn + 2, lastColumn));
                sheet.Cells[footerRow, preparedColumn] = $"填表人：{options.PreparedBy}";
            }
            if (reviewedColumn > preparedColumn)
            {
                Merge(sheet, footerRow, reviewedColumn, footerRow, Math.Min(reviewedColumn + 1, lastColumn));
                sheet.Cells[footerRow, reviewedColumn] = $"审核员：{options.ReviewedBy}";
            }
            if (dateColumn > reviewedColumn)
            {
                Merge(sheet, footerRow, dateColumn, footerRow, lastColumn);
                Excel.Range dateRange = null;
                try
                {
                    dateRange = sheet.Range[sheet.Cells[footerRow, dateColumn], sheet.Cells[footerRow, lastColumn]];
                    dateRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignRight;
                    dateRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                }
                finally
                {
                    ReleaseComObject(dateRange);
                }

                sheet.Cells[footerRow, dateColumn] = $"填表日期:{options.ReportDate}";
            }

            FormatSheet(sheet, lastColumn, totalRow, footerRow, options.DecimalPlaces, totalColumn, plotColumn, unitColumn);
            ApplyPageSetup(sheet, lastColumn, footerRow);
        }


        private static void ApplyBaseLayout(Excel.Worksheet sheet, int lastColumn, int footerRow, bool includePlotName)
        {
            SetColumnWidth(sheet, 1, 4.39);
            SetColumnWidth(sheet, 2, 7.53);
            if (includePlotName)
            {
                SetColumnWidth(sheet, 3, 15.0);
                SetColumnWidth(sheet, 4, 21.12);
                SetColumnWidth(sheet, 5, 8.87);
                SetColumnWidth(sheet, 6, 8.5);
                for (int column = 7; column <= lastColumn; column++)
                {
                    SetColumnWidth(sheet, column, 8.5);
                }
            }
            else
            {
                SetColumnWidth(sheet, 3, 21.12);
                SetColumnWidth(sheet, 4, 8.87);
                SetColumnWidth(sheet, 5, 8.5);
                for (int column = 6; column <= lastColumn; column++)
                {
                    SetColumnWidth(sheet, column, 8.5);
                }
            }

            SetRowHeight(sheet, 1, 12.0);
            SetRowHeight(sheet, 2, 18.0);
            SetRowHeight(sheet, 3, 18.0);
            SetRowHeight(sheet, 4, 18.0);
            SetRowHeight(sheet, 5, 25.0);
            SetRowHeight(sheet, 6, 25.0);
            SetRowHeight(sheet, 7, 25.0);
            SetRowHeight(sheet, 8, 25.0);
            for (int row = 8; row < footerRow; row++)
            {
                SetRowHeight(sheet, row, 25.0);
            }

            SetRowHeight(sheet, footerRow, 18.0);
        }


        private static void WriteFixedHeaders(Excel.Worksheet sheet, int firstColumn, bool includePlotName)
        {
            Merge(sheet, 5, firstColumn, 7, firstColumn);
            sheet.Cells[5, firstColumn] = "序号";
            int offset = 1;
            if (includePlotName)
            {
                Merge(sheet, 5, firstColumn + offset, 7, firstColumn + offset);
                sheet.Cells[5, firstColumn + offset] = "地块名称";
                offset++;
            }

            Merge(sheet, 5, firstColumn + offset, 7, firstColumn + offset);
            Merge(sheet, 5, firstColumn + offset + 1, 7, firstColumn + offset + 1);
            Merge(sheet, 5, firstColumn + offset + 2, 7, firstColumn + offset + 2);
            sheet.Cells[5, firstColumn + offset] = "权属单位";
            sheet.Cells[5, firstColumn + offset + 1] = "权属类别";
            sheet.Cells[5, firstColumn + offset + 2] = "总计";
        }


        private static void WriteClassHeaders(
            Excel.Worksheet sheet,
            IReadOnlyList<LandClassTableColumn> columns,
            int startColumn)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                int excelColumn = startColumn + i;
                sheet.Cells[5, excelColumn] = column.TopGroupText;
                sheet.Cells[6, excelColumn] = column.Level == LandClassTableColumnLevel.TopGroup
                    ? "合计"
                    : column.SecondGroupText;
                sheet.Cells[7, excelColumn] = column.Level switch
                {
                    LandClassTableColumnLevel.TopGroup => string.Empty,
                    LandClassTableColumnLevel.SecondGroup => HasLeafChildren(columns, column.Key) ? "小计" : string.Empty,
                    _ => column.HeaderText
                };
            }

            MergeConsecutiveHeaders(sheet, columns, startColumn, 5, x => x.TopGroupKey);
            MergeConsecutiveHeaders(sheet, columns, startColumn, 6, x => x.Level == LandClassTableColumnLevel.TopGroup ? x.Key : x.SecondGroupKey);
            foreach (var item in columns.Select((column, index) => new { column, index }).Where(x => x.column.Level == LandClassTableColumnLevel.TopGroup))
            {
                Merge(sheet, 6, startColumn + item.index, 7, startColumn + item.index);
            }

            foreach (var item in columns.Select((column, index) => new { column, index }).Where(x => x.column.Level == LandClassTableColumnLevel.SecondGroup))
            {
                Merge(sheet, 7, startColumn + item.index, 7, startColumn + item.index);
            }
        }


        private static void MergeConsecutiveHeaders(
            Excel.Worksheet sheet,
            IReadOnlyList<LandClassTableColumn> columns,
            int startColumn,
            int row,
            Func<LandClassTableColumn, string> keySelector)
        {
            int index = 0;
            while (index < columns.Count)
            {
                string key = keySelector(columns[index]);
                int end = index;
                while (end + 1 < columns.Count && keySelector(columns[end + 1]) == key)
                {
                    end++;
                }

                if (end > index)
                {
                    Merge(sheet, row, startColumn + index, row, startColumn + end);
                }

                index = end + 1;
            }
        }

        private static void ThrowIfCancelled(Func<bool> isCancelRequested)
        {
            if (isCancelRequested())
            {
                throw new OperationCanceledException();
            }
        }

        private static string NormalizeGroupValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未命名" : value.Trim();
        }

        private static string EnsureUniqueOutputPath(string outputPath)
        {
            string folder = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
            string fileName = Path.GetFileNameWithoutExtension(outputPath);
            string extension = Path.GetExtension(outputPath);
            string candidate = Path.Combine(folder, fileName + extension);
            int counter = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(folder, $"{fileName}_{counter}{extension}");
                counter++;
            }

            return candidate;
        }

        private static string NormalizeOutputPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "未命名";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Trim().Where(ch => !invalid.Contains(ch)).ToArray();
            var text = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(text) ? "未命名" : text;
        }

        private static void SetColumnWidth(Excel.Worksheet sheet, int column, double width)
        {
            Excel.Range range = null;
            try
            {
                range = (Excel.Range)sheet.Columns[column];
                range.ColumnWidth = width;
            }
            finally
            {
                ReleaseComObject(range);
            }
        }

        private static void SetRowHeight(Excel.Worksheet sheet, int row, double height)
        {
            Excel.Range range = null;
            try
            {
                range = (Excel.Range)sheet.Rows[row];
                range.RowHeight = height;
            }
            finally
            {
                ReleaseComObject(range);
            }
        }

        private static double RoundArea(double value, int decimalPlaces)
        {
            return Math.Abs(value) < Math.Pow(10, -decimalPlaces) / 2
                ? 0
                : Math.Round(value, decimalPlaces);
        }

        private static string GetColumnName(int columnNumber)
        {
            int dividend = columnNumber;
            string columnName = string.Empty;
            while (dividend > 0)
            {
                int modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }

            return columnName;
        }

        private static bool IsChildColumn(
            IReadOnlyList<LandClassTableColumn> columns,
            LandClassTableColumn parent,
            LandClassTableColumn child)
        {
            if (parent.Level == LandClassTableColumnLevel.TopGroup)
            {
                return child.TopGroupKey.Equals(parent.Key, StringComparison.OrdinalIgnoreCase) &&
                       (child.Level == LandClassTableColumnLevel.SecondGroup ||
                        child.Level == LandClassTableColumnLevel.Leaf && !columns.Any(x =>
                            x.Level == LandClassTableColumnLevel.SecondGroup &&
                            x.Key.Equals(child.SecondGroupKey, StringComparison.OrdinalIgnoreCase)));
            }

            return parent.Level == LandClassTableColumnLevel.SecondGroup &&
                   child.Level == LandClassTableColumnLevel.Leaf &&
                   child.SecondGroupKey.Equals(parent.Key, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasLeafChildren(IReadOnlyList<LandClassTableColumn> columns, string secondGroupKey)
        {
            return columns.Any(x =>
                x.Level == LandClassTableColumnLevel.Leaf &&
                x.SecondGroupKey.Equals(secondGroupKey, StringComparison.OrdinalIgnoreCase));
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null)
            {
                try { Marshal.ReleaseComObject(value); } catch { }
            }
        }

    }
}
