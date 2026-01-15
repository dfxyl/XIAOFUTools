using System;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Tools.IntersectSummary
{
    /// <summary>
    /// 交集汇总结果窗口
    /// </summary>
    public partial class IntersectSummaryResultWindow : Window
    {
        private DataTable _dataTable;
        private int _decimalPlaces;
        private int _regionFieldCount;
        private Action _exportExcelAction;

        public IntersectSummaryResultWindow(DataTable dataTable, int decimalPlaces, int regionFieldCount = 0, Action exportExcelAction = null)
        {
            InitializeComponent();
            _dataTable = dataTable;
            _decimalPlaces = decimalPlaces;
            _regionFieldCount = regionFieldCount;
            _exportExcelAction = exportExcelAction;

            ResultDataGrid.ItemsSource = dataTable.DefaultView;
            InfoText.Text = $"共 {dataTable.Rows.Count} 条记录，{dataTable.Columns.Count} 列";
        }

        /// <summary>
        /// 复制选中单元格
        /// </summary>
        private void CopySelectedCells_Click(object sender, RoutedEventArgs e)
        {
            if (ResultDataGrid.SelectedCells.Count > 0)
            {
                ApplicationCommands.Copy.Execute(null, ResultDataGrid);
            }
        }

        /// <summary>
        /// 复制全部数据
        /// </summary>
        private void CopyAllData_Click(object sender, RoutedEventArgs e)
        {
            if (_dataTable == null || _dataTable.Rows.Count == 0)
            {
                MessageBox.Show("没有数据可复制", "提示");
                return;
            }

            try
            {
                var sb = new StringBuilder();

                // 添加列标题
                for (int i = 0; i < _dataTable.Columns.Count; i++)
                {
                    if (i > 0) sb.Append("\t");
                    sb.Append(_dataTable.Columns[i].ColumnName);
                }
                sb.AppendLine();

                // 添加数据行
                foreach (DataRow row in _dataTable.Rows)
                {
                    for (int i = 0; i < _dataTable.Columns.Count; i++)
                    {
                        if (i > 0) sb.Append("\t");
                        var value = row[i];
                        if (value is double d)
                        {
                            sb.Append(Math.Round(d, _decimalPlaces).ToString($"F{_decimalPlaces}"));
                        }
                        else
                        {
                            sb.Append(value?.ToString() ?? "");
                        }
                    }
                    sb.AppendLine();
                }

                Clipboard.SetText(sb.ToString());
                MessageBox.Show($"已复制 {_dataTable.Rows.Count} 行数据到剪贴板", "成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 全选
        /// </summary>
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            ResultDataGrid.SelectAll();
        }

        /// <summary>
        /// 导出Excel
        /// </summary>
        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel文件 (*.xlsx)|*.xlsx|CSV文件 (*.csv)|*.csv",
                DefaultExt = ".xlsx",
                FileName = $"交集汇总表_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = saveDialog.FileName;
                    string extension = System.IO.Path.GetExtension(filePath).ToLower();

                    if (extension == ".csv")
                    {
                        ExportToCsv(filePath);
                    }
                    else
                    {
                        ExportToExcel(filePath);
                    }

                    MessageBox.Show($"导出成功!\n{filePath}", "成功");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误");
                }
            }
        }

        /// <summary>
        /// 导出为CSV
        /// </summary>
        private void ExportToCsv(string filePath)
        {
            using (var writer = new System.IO.StreamWriter(filePath, false, Encoding.UTF8))
            {
                // 写入列标题
                var columnNames = new System.Collections.Generic.List<string>();
                foreach (DataColumn col in _dataTable.Columns)
                {
                    columnNames.Add($"\"{col.ColumnName}\"");
                }
                writer.WriteLine(string.Join(",", columnNames));

                // 写入数据行
                foreach (DataRow row in _dataTable.Rows)
                {
                    var values = new System.Collections.Generic.List<string>();
                    foreach (var item in row.ItemArray)
                    {
                        if (item is double d)
                        {
                            values.Add(d.ToString($"F{_decimalPlaces}"));
                        }
                        else
                        {
                            values.Add($"\"{item}\"");
                        }
                    }
                    writer.WriteLine(string.Join(",", values));
                }
            }
        }

        /// <summary>
        /// 导出为Excel
        /// </summary>
        private void ExportToExcel(string filePath)
        {
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet worksheet = null;

            try
            {
                excelApp = new Excel.Application();
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;

                workbook = excelApp.Workbooks.Add();
                worksheet = (Excel.Worksheet)workbook.Sheets[1];
                worksheet.Name = "交集汇总表";

                // 写入列标题
                for (int col = 0; col < _dataTable.Columns.Count; col++)
                {
                    worksheet.Cells[1, col + 1] = _dataTable.Columns[col].ColumnName;
                }

                // 设置标题行样式
                Excel.Range headerRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[1, _dataTable.Columns.Count]];
                headerRange.Font.Bold = true;
                headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(79, 129, 189));
                headerRange.Font.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.White);
                headerRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

                int dataRowCount = _dataTable.Rows.Count;
                int lastDataRow = dataRowCount;

                // 写入数据行
                for (int row = 0; row < dataRowCount; row++)
                {
                    for (int col = 0; col < _dataTable.Columns.Count; col++)
                    {
                        var value = _dataTable.Rows[row][col];
                        if (value is double d)
                        {
                            worksheet.Cells[row + 2, col + 1] = Math.Round(d, _decimalPlaces);
                            string format = _decimalPlaces > 0 ? $"0.{new string('0', _decimalPlaces)}" : "0";
                            ((Excel.Range)worksheet.Cells[row + 2, col + 1]).NumberFormat = format;
                        }
                        else
                        {
                            worksheet.Cells[row + 2, col + 1] = value?.ToString() ?? "";
                        }
                    }
                }

                // 合并相同值的单元格（按列处理，跳过最后一行合计和面积列）
                int mergeableColumns = _dataTable.Columns.Count - 1; // 面积列不合并
                int dataRows = lastDataRow - 1; // 不含合计行的数据行数

                // 辅助函数：获取指定行的区域键（用于判断是否属于同一分组）
                Func<int, string> getRegionKey = (rowIndex) =>
                {
                    if (_regionFieldCount == 0) return "";
                    var keys = new System.Collections.Generic.List<string>();
                    for (int c = 0; c < _regionFieldCount; c++)
                    {
                        keys.Add(_dataTable.Rows[rowIndex][c]?.ToString() ?? "");
                    }
                    return string.Join("|", keys);
                };

                for (int col = 0; col < mergeableColumns; col++)
                {
                    bool isRegionColumn = col < _regionFieldCount; // 是否为区域字段列
                    int mergeStartRow = 0; // 数据行索引（0-based）
                    string currentValue = _dataTable.Rows[0][col]?.ToString() ?? "";
                    string mergeStartRegionKey = getRegionKey(0);

                    for (int row = 1; row <= dataRows; row++)
                    {
                        bool shouldEndMerge = false;
                        string cellValue = row < dataRows ? (_dataTable.Rows[row][col]?.ToString() ?? "") : "";
                        string currentRegionKey = row < dataRows ? getRegionKey(row) : "";

                        if (row == dataRows)
                        {
                            shouldEndMerge = true;
                        }
                        else if (cellValue == "合计")
                        {
                            shouldEndMerge = true;
                        }
                        else if (cellValue != currentValue)
                        {
                            shouldEndMerge = true;
                        }
                        else if (!isRegionColumn && currentRegionKey != mergeStartRegionKey)
                        {
                            // 类字段列：区域分组变化，不能跨组合并
                            shouldEndMerge = true;
                        }

                        if (shouldEndMerge)
                        {
                            if (row > mergeStartRow + 1)
                            {
                                try
                                {
                                    int excelStartRow = mergeStartRow + 2;
                                    int excelEndRow = row + 1;
                                    Excel.Range mergeRange = worksheet.Range[
                                        worksheet.Cells[excelStartRow, col + 1],
                                        worksheet.Cells[excelEndRow, col + 1]];
                                    mergeRange.Merge();
                                    mergeRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                                }
                                catch { }
                            }

                            mergeStartRow = row;
                            currentValue = cellValue;
                            mergeStartRegionKey = currentRegionKey;
                        }
                    }
                }

                // 设置合计行样式（最后一行）
                if (dataRowCount > 0)
                {
                    Excel.Range totalRowRange = worksheet.Range[
                        worksheet.Cells[dataRowCount + 1, 1],
                        worksheet.Cells[dataRowCount + 1, _dataTable.Columns.Count]];
                    totalRowRange.Font.Bold = true;
                    totalRowRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(221, 235, 247));
                }

                // 自动调整列宽
                worksheet.Columns.AutoFit();

                // 添加边框
                Excel.Range dataRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[dataRowCount + 1, _dataTable.Columns.Count]];
                dataRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                dataRange.Borders.Weight = Excel.XlBorderWeight.xlThin;

                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook);
            }
            finally
            {
                if (workbook != null)
                {
                    workbook.Close(false);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                }
                if (excelApp != null)
                {
                    excelApp.Quit();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                }
                if (worksheet != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
