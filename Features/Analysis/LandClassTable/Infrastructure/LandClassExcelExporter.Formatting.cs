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

        private static void MergePlotNameCells(
            Excel.Worksheet sheet,
            IReadOnlyList<LandClassTableRow> rows,
            int firstDataRow,
            int plotColumn)
        {
            foreach (LandClassTableRowRange range in LandClassTableRowGrouping.GetPlotNameMergeRanges(rows))
            {
                Excel.Range plotRange = null;
                try
                {
                    int startRow = firstDataRow + range.StartIndex;
                    int endRow = firstDataRow + range.EndIndex;
                    plotRange = sheet.Range[sheet.Cells[startRow, plotColumn], sheet.Cells[endRow, plotColumn]];
                    plotRange.Merge();
                    plotRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    plotRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                }
                finally
                {
                    ReleaseComObject(plotRange);
                }
            }
        }

        private static void FormatSheet(
            Excel.Worksheet sheet,
            int lastColumn,
            int totalRow,
            int footerRow,
            int decimalPlaces,
            int firstNumberColumn,
            int plotColumn,
            int unitColumn)
        {
            int firstColumn = 2;
            Excel.Range titleRange = null;
            Excel.Range headerRange = null;
            Excel.Range tableRange = null;
            Excel.Range allRange = null;
            Excel.Range numberRange = null;
            Excel.Range plotRange = null;
            Excel.Range unitRange = null;
            try
            {
                allRange = sheet.Range[sheet.Cells[1, firstColumn], sheet.Cells[footerRow, lastColumn]];
                allRange.Font.Name = "宋体";
                allRange.Font.Size = 9;

                titleRange = sheet.Range[sheet.Cells[2, firstColumn], sheet.Cells[3, lastColumn]];
                titleRange.Font.Bold = true;
                titleRange.Font.Size = 12;
                titleRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                titleRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;

                headerRange = sheet.Range[sheet.Cells[5, firstColumn], sheet.Cells[7, lastColumn]];
                headerRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                headerRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                headerRange.WrapText = true;

                tableRange = sheet.Range[sheet.Cells[5, firstColumn], sheet.Cells[totalRow, lastColumn]];
                tableRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                tableRange.Borders.Weight = Excel.XlBorderWeight.xlThin;
                tableRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                tableRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;

                numberRange = sheet.Range[sheet.Cells[8, firstNumberColumn], sheet.Cells[totalRow, lastColumn]];
                string decimalPart = decimalPlaces > 0 ? "." + new string('0', decimalPlaces) : string.Empty;
                numberRange.NumberFormat = $"0{decimalPart};-0{decimalPart};;@";

                if (plotColumn > 0)
                {
                    plotRange = sheet.Range[sheet.Cells[5, plotColumn], sheet.Cells[totalRow, plotColumn]];
                    plotRange.Columns.AutoFit();
                }

                unitRange = sheet.Range[sheet.Cells[5, unitColumn], sheet.Cells[totalRow, unitColumn]];
                unitRange.WrapText = true;
            }
            finally
            {
                ReleaseComObject(titleRange);
                ReleaseComObject(headerRange);
                ReleaseComObject(tableRange);
                ReleaseComObject(allRange);
                ReleaseComObject(numberRange);
                ReleaseComObject(plotRange);
                ReleaseComObject(unitRange);
            }
        }


        private static void ApplyPageSetup(Excel.Worksheet sheet, int lastColumn, int footerRow)
        {
            Excel.PageSetup pageSetup = null;
            try
            {
                pageSetup = sheet.PageSetup;
                pageSetup.PaperSize = Excel.XlPaperSize.xlPaperA4;
                pageSetup.Orientation = Excel.XlPageOrientation.xlLandscape;
                pageSetup.Zoom = false;
                pageSetup.FitToPagesWide = 1;
                pageSetup.FitToPagesTall = false;
                pageSetup.CenterHorizontally = true;
                pageSetup.CenterVertically = false;
                pageSetup.LeftMargin = sheet.Application.CentimetersToPoints(1.0);
                pageSetup.RightMargin = sheet.Application.CentimetersToPoints(1.0);
                pageSetup.TopMargin = sheet.Application.CentimetersToPoints(1.0);
                pageSetup.BottomMargin = sheet.Application.CentimetersToPoints(1.0);
                pageSetup.PrintArea = sheet.Range[sheet.Cells[1, 2], sheet.Cells[footerRow, lastColumn]].Address;
            }
            finally
            {
                ReleaseComObject(pageSetup);
            }
        }


        private static void WriteRowFormulas(
            Excel.Worksheet sheet,
            IReadOnlyList<LandClassTableColumn> columns,
            int row,
            int totalColumn,
            int classStartColumn)
        {
            var indexByKey = columns
                .Select((column, index) => new { column.Key, Index = index })
                .ToDictionary(x => x.Key, x => classStartColumn + x.Index, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                int excelColumn = classStartColumn + i;
                if (column.Level == LandClassTableColumnLevel.Leaf)
                {
                    continue;
                }

                if (CanWriteDirectValue(columns, column))
                {
                    continue;
                }

                var childColumns = columns
                    .Where(x => IsChildColumn(columns, column, x))
                    .Where(x => indexByKey.ContainsKey(x.Key))
                    .Select(x => $"{GetColumnName(indexByKey[x.Key])}{row}")
                    .Distinct()
                    .ToList();
                sheet.Cells[row, excelColumn] = childColumns.Count > 0
                    ? "=" + string.Join("+", childColumns)
                    : 0;
            }

            var topGroupColumns = columns
                .Where(x => x.Level == LandClassTableColumnLevel.TopGroup)
                .Where(x => indexByKey.ContainsKey(x.Key))
                .Select(x => $"{GetColumnName(indexByKey[x.Key])}{row}")
                .ToList();
            sheet.Cells[row, totalColumn] = topGroupColumns.Count > 0
                ? "=" + string.Join("+", topGroupColumns)
                : 0;
        }


        private static bool CanWriteDirectValue(IReadOnlyList<LandClassTableColumn> columns, LandClassTableColumn column)
        {
            if (column.Level == LandClassTableColumnLevel.Leaf)
            {
                return true;
            }

            if (column.Level == LandClassTableColumnLevel.SecondGroup)
            {
                return !columns.Any(x =>
                    x.Level == LandClassTableColumnLevel.Leaf &&
                    x.SecondGroupKey.Equals(column.Key, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }


        private static void Merge(Excel.Worksheet sheet, int row1, int col1, int row2, int col2)
        {
            Excel.Range range = null;
            try
            {
                range = sheet.Range[sheet.Cells[row1, col1], sheet.Cells[row2, col2]];
                range.Merge();
            }
            finally
            {
                ReleaseComObject(range);
            }
        }


    }
}
