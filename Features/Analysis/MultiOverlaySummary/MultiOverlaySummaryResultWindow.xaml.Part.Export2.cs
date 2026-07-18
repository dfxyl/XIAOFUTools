using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Features.Analysis.MultiOverlaySummary
{
    public partial class MultiOverlaySummaryResultWindow
    {

        private void ExportToExcel(string filePath)
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
            Excel.Range totalCell1 = null;
            Excel.Range totalCell2 = null;
            Excel.Range columns = null;
            Excel.Range dataRange = null;
            Excel.Range dataCell1 = null;
            Excel.Range dataCell2 = null;
            Excel.Borders borders = null;

            try
            {
                excelApp = new Excel.Application { Visible = false, DisplayAlerts = false };
                workbooks = excelApp.Workbooks;
                workbook = workbooks.Add();
                sheets = workbook.Sheets;
                worksheet = (Excel.Worksheet)sheets[1];
                worksheet.Name = "多图层压盖汇总表";

                // 写入表头
                for (int col = 0; col < _dataTable.Columns.Count; col++)
                    SetCellValue(worksheet, 1, col + 1, _dataTable.Columns[col].ColumnName);

                // 设置表头样式
                headerCell1 = (Excel.Range)worksheet.Cells[1, 1];
                headerCell2 = (Excel.Range)worksheet.Cells[1, _dataTable.Columns.Count];
                headerRange = worksheet.Range[headerCell1, headerCell2];
                headerRange.Font.Bold = true;
                headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(79, 129, 189));
                headerRange.Font.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.White);
                headerRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

                // 写入数据行
                string numberFormat = _decimalPlaces > 0 ? $"0.{new string('0', _decimalPlaces)}" : "0";
                for (int row = 0; row < _dataTable.Rows.Count; row++)
                {
                    for (int col = 0; col < _dataTable.Columns.Count; col++)
                    {
                        var value = _dataTable.Rows[row][col];
                        if (value is double d)
                            SetCellValueWithFormat(worksheet, row + 2, col + 1, Math.Round(d, _decimalPlaces), numberFormat);
                        else
                            SetCellValue(worksheet, row + 2, col + 1, value?.ToString() ?? "");
                    }
                }

                // 设置合计行样式
                if (_dataTable.Rows.Count > 0)
                {
                    totalCell1 = (Excel.Range)worksheet.Cells[_dataTable.Rows.Count + 1, 1];
                    totalCell2 = (Excel.Range)worksheet.Cells[_dataTable.Rows.Count + 1, _dataTable.Columns.Count];
                    totalRowRange = worksheet.Range[totalCell1, totalCell2];
                    totalRowRange.Font.Bold = true;
                    totalRowRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(221, 235, 247));
                }

                // 自动列宽
                columns = worksheet.Columns;
                columns.AutoFit();

                // 设置边框
                dataCell1 = (Excel.Range)worksheet.Cells[1, 1];
                dataCell2 = (Excel.Range)worksheet.Cells[_dataTable.Rows.Count + 1, _dataTable.Columns.Count];
                dataRange = worksheet.Range[dataCell1, dataCell2];
                borders = dataRange.Borders;
                borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                borders.Weight = Excel.XlBorderWeight.xlThin;

                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook);
            }
            finally
            {
                // 按从子到父的顺序释放 COM 对象，每个都有独立 try-catch
                try { if (borders != null) Marshal.ReleaseComObject(borders); } catch { }
                try { if (dataCell2 != null) Marshal.ReleaseComObject(dataCell2); } catch { }
                try { if (dataCell1 != null) Marshal.ReleaseComObject(dataCell1); } catch { }
                try { if (dataRange != null) Marshal.ReleaseComObject(dataRange); } catch { }
                try { if (columns != null) Marshal.ReleaseComObject(columns); } catch { }
                try { if (totalCell2 != null) Marshal.ReleaseComObject(totalCell2); } catch { }
                try { if (totalCell1 != null) Marshal.ReleaseComObject(totalCell1); } catch { }
                try { if (totalRowRange != null) Marshal.ReleaseComObject(totalRowRange); } catch { }
                try { if (headerCell2 != null) Marshal.ReleaseComObject(headerCell2); } catch { }
                try { if (headerCell1 != null) Marshal.ReleaseComObject(headerCell1); } catch { }
                try { if (headerRange != null) Marshal.ReleaseComObject(headerRange); } catch { }
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
