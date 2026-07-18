using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Features.Analysis.IntersectSummary.Infrastructure
{
    internal static class IntersectSummaryExcelExporter
    {

        /// <summary>
        /// 导出为Excel
        /// </summary>
        internal static void Export(DataTable resultTable, string filePath, int decimalPlaces, int regionFieldCount)
        {
            Excel.Application excelApp = null;
            Excel.Workbooks workbooks = null;
            Excel.Workbook workbook = null;
            Excel.Sheets sheets = null;
            Excel.Worksheet worksheet = null;
            Excel.Range headerRange = null;
            Excel.Range headerCell1 = null;
            Excel.Range headerCell2 = null;
            Excel.Range totalRowRange = null;
            Excel.Range totalRowCell1 = null;
            Excel.Range totalRowCell2 = null;
            Excel.Range dataRange = null;
            Excel.Range dataRangeCell1 = null;
            Excel.Range dataRangeCell2 = null;
            Excel.Range columns = null;

            try
            {
                // 创建Excel应用程序
                excelApp = new Excel.Application();
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;

                // 创建工作簿和工作表
                workbooks = excelApp.Workbooks;
                workbook = workbooks.Add();
                sheets = workbook.Sheets;
                worksheet = (Excel.Worksheet)sheets[1];
                worksheet.Name = "交集汇总表";

                // 写入列标题
                for (int col = 0; col < resultTable.Columns.Count; col++)
                {
                    Excel.Range cell = (Excel.Range)worksheet.Cells[1, col + 1];
                    try
                    {
                        cell.Value2 = resultTable.Columns[col].ColumnName;
                    }
                    finally
                    {
                        if (cell != null) Marshal.ReleaseComObject(cell);
                    }
                }

                // 设置标题行样式
                headerCell1 = (Excel.Range)worksheet.Cells[1, 1];
                headerCell2 = (Excel.Range)worksheet.Cells[1, resultTable.Columns.Count];
                headerRange = worksheet.Range[headerCell1, headerCell2];
                headerRange.Font.Bold = true;
                headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(79, 129, 189));
                headerRange.Font.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.White);
                headerRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

                int dataRowCount = resultTable.Rows.Count;
                int lastDataRow = dataRowCount; // 不含合计行的最后一行索引（0-based）

                // 写入数据行
                for (int row = 0; row < dataRowCount; row++)
                {
                    for (int col = 0; col < resultTable.Columns.Count; col++)
                    {
                        Excel.Range cell = (Excel.Range)worksheet.Cells[row + 2, col + 1];
                        try
                        {
                            var value = resultTable.Rows[row][col];
                            if (value is double d)
                            {
                                cell.Value2 = Math.Round(d, decimalPlaces);
                                // 设置数值格式
                                string format = decimalPlaces > 0 ? $"0.{new string('0', decimalPlaces)}" : "0";
                                cell.NumberFormat = format;
                            }
                            else
                            {
                                cell.Value2 = value?.ToString() ?? "";
                            }
                        }
                        finally
                        {
                            if (cell != null) Marshal.ReleaseComObject(cell);
                        }
                    }
                }

                // 合并相同值的单元格（按列处理，跳过最后一行合计和面积列）
                int mergeableColumns = resultTable.Columns.Count - 1; // 面积列不合并
                int dataRows = lastDataRow - 1; // 不含合计行的数据行数

                // 辅助函数：获取指定行的区域键（用于判断是否属于同一分组）
                Func<int, string> getRegionKey = (rowIndex) =>
                {
                    if (regionFieldCount == 0) return "";
                    var keys = new List<string>();
                    for (int c = 0; c < regionFieldCount; c++)
                    {
                        keys.Add(resultTable.Rows[rowIndex][c]?.ToString() ?? "");
                    }
                    return string.Join("|", keys);
                };

                for (int col = 0; col < mergeableColumns; col++)
                {
                    bool isRegionColumn = col < regionFieldCount; // 是否为区域字段列
                    int mergeStartRow = 0; // 数据行索引（0-based）
                    string currentValue = resultTable.Rows[0][col]?.ToString() ?? "";
                    string mergeStartRegionKey = getRegionKey(0);

                    for (int row = 1; row <= dataRows; row++)
                    {
                        bool shouldEndMerge = false;
                        string cellValue = row < dataRows ? (resultTable.Rows[row][col]?.ToString() ?? "") : "";
                        string currentRegionKey = row < dataRows ? getRegionKey(row) : "";

                        if (row == dataRows)
                        {
                            // 到达合计行，结束合并
                            shouldEndMerge = true;
                        }
                        else if (cellValue == "合计")
                        {
                            // 遇到合计行
                            shouldEndMerge = true;
                        }
                        else if (cellValue != currentValue)
                        {
                            // 值不同
                            shouldEndMerge = true;
                        }
                        else if (!isRegionColumn && currentRegionKey != mergeStartRegionKey)
                        {
                            // 类字段列：区域分组变化，不能跨组合并
                            shouldEndMerge = true;
                        }

                        if (shouldEndMerge)
                        {
                            // 执行合并（如果有多行）
                            if (row > mergeStartRow + 1)
                            {
                                Excel.Range mergeCell1 = null;
                                Excel.Range mergeCell2 = null;
                                Excel.Range mergeRange = null;
                                try
                                {
                                    int excelStartRow = mergeStartRow + 2; // Excel行从2开始
                                    int excelEndRow = row + 1; // 合并到当前行之前
                                    mergeCell1 = (Excel.Range)worksheet.Cells[excelStartRow, col + 1];
                                    mergeCell2 = (Excel.Range)worksheet.Cells[excelEndRow, col + 1];
                                    mergeRange = worksheet.Range[mergeCell1, mergeCell2];
                                    mergeRange.Merge();
                                    mergeRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                                }
                                catch { }
                                finally
                                {
                                    try { if (mergeRange != null) Marshal.ReleaseComObject(mergeRange); } catch { }
                                    try { if (mergeCell2 != null) Marshal.ReleaseComObject(mergeCell2); } catch { }
                                    try { if (mergeCell1 != null) Marshal.ReleaseComObject(mergeCell1); } catch { }
                                }
                            }

                            // 重置起始位置
                            mergeStartRow = row;
                            currentValue = cellValue;
                            mergeStartRegionKey = currentRegionKey;
                        }
                    }
                }

                // 设置合计行样式（最后一行）
                if (dataRowCount > 0)
                {
                    totalRowCell1 = (Excel.Range)worksheet.Cells[dataRowCount + 1, 1];
                    totalRowCell2 = (Excel.Range)worksheet.Cells[dataRowCount + 1, resultTable.Columns.Count];
                    totalRowRange = worksheet.Range[totalRowCell1, totalRowCell2];
                    totalRowRange.Font.Bold = true;
                    totalRowRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(221, 235, 247));
                }

                // 自动调整列宽
                columns = (Excel.Range)worksheet.Columns;
                columns.AutoFit();

                // 添加边框
                dataRangeCell1 = (Excel.Range)worksheet.Cells[1, 1];
                dataRangeCell2 = (Excel.Range)worksheet.Cells[dataRowCount + 1, resultTable.Columns.Count];
                dataRange = worksheet.Range[dataRangeCell1, dataRangeCell2];
                dataRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                dataRange.Borders.Weight = Excel.XlBorderWeight.xlThin;

                // 保存文件
                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook);
            }
            finally
            {
                // 按正确顺序释放 COM 对象：Range -> Worksheet -> Workbook -> Application
                try { if (columns != null) Marshal.ReleaseComObject(columns); } catch { }
                try { if (dataRange != null) Marshal.ReleaseComObject(dataRange); } catch { }
                try { if (dataRangeCell2 != null) Marshal.ReleaseComObject(dataRangeCell2); } catch { }
                try { if (dataRangeCell1 != null) Marshal.ReleaseComObject(dataRangeCell1); } catch { }
                try { if (totalRowRange != null) Marshal.ReleaseComObject(totalRowRange); } catch { }
                try { if (totalRowCell2 != null) Marshal.ReleaseComObject(totalRowCell2); } catch { }
                try { if (totalRowCell1 != null) Marshal.ReleaseComObject(totalRowCell1); } catch { }
                try { if (headerRange != null) Marshal.ReleaseComObject(headerRange); } catch { }
                try { if (headerCell2 != null) Marshal.ReleaseComObject(headerCell2); } catch { }
                try { if (headerCell1 != null) Marshal.ReleaseComObject(headerCell1); } catch { }
                try { if (worksheet != null) Marshal.ReleaseComObject(worksheet); } catch { }
                try { if (sheets != null) Marshal.ReleaseComObject(sheets); } catch { }
                try { if (workbook != null) { workbook.Close(false); Marshal.ReleaseComObject(workbook); } } catch { }
                try { if (workbooks != null) Marshal.ReleaseComObject(workbooks); } catch { }
                try { if (excelApp != null) { excelApp.Quit(); Marshal.ReleaseComObject(excelApp); } } catch { }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }
}
