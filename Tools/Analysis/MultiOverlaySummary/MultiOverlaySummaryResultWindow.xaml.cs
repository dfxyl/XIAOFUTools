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

namespace XIAOFUTools.Tools.MultiOverlaySummary
{
    public partial class MultiOverlaySummaryResultWindow : Window
    {
        private DataTable _dataTable;
        private int _decimalPlaces;
        private List<IntersectGeometryItem> _intersectGeometries;
        private SpatialReference _spatialReference;
        private string _areaUnit;

        public MultiOverlaySummaryResultWindow(DataTable dataTable, int decimalPlaces, 
            List<IntersectGeometryItem> intersectGeometries = null, SpatialReference spatialReference = null,
            string areaUnit = "平方米")
        {
            InitializeComponent();
            _dataTable = dataTable;
            _decimalPlaces = decimalPlaces;
            _intersectGeometries = intersectGeometries;
            _spatialReference = spatialReference;
            _areaUnit = areaUnit;

            ResultDataGrid.ItemsSource = dataTable.DefaultView;
            InfoText.Text = $"共 {dataTable.Rows.Count} 条记录，{dataTable.Columns.Count} 列";
        }

        private void CopySelectedCells_Click(object sender, RoutedEventArgs e)
        {
            if (ResultDataGrid.SelectedCells.Count > 0)
                ApplicationCommands.Copy.Execute(null, ResultDataGrid);
        }

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
                for (int i = 0; i < _dataTable.Columns.Count; i++)
                {
                    if (i > 0) sb.Append("\t");
                    sb.Append(_dataTable.Columns[i].ColumnName);
                }
                sb.AppendLine();

                foreach (DataRow row in _dataTable.Rows)
                {
                    for (int i = 0; i < _dataTable.Columns.Count; i++)
                    {
                        if (i > 0) sb.Append("\t");
                        var value = row[i];
                        sb.Append(value is double d ? Math.Round(d, _decimalPlaces).ToString($"F{_decimalPlaces}") : value?.ToString() ?? "");
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

        private void SelectAll_Click(object sender, RoutedEventArgs e) => ResultDataGrid.SelectAll();

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ExportOptionsDialog();
            if (dialog.ShowDialog() != true)
                return;

            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择导出文件夹",
                ShowNewFolderButton = true
            };

            if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            string outputFolder = folderDialog.SelectedPath;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var results = new List<string>();

            try
            {
                if (dialog.ExportExcel)
                {
                    string excelPath = Path.Combine(outputFolder, $"多图层压盖汇总表_{timestamp}.xlsx");
                    ExportToExcel(excelPath);
                    results.Add($"表格: {excelPath}");
                }

                if (dialog.ExportGdb && _intersectGeometries?.Count > 0)
                {
                    string gdbPath = await ExportToGdbAsync(outputFolder, timestamp);
                    if (!string.IsNullOrEmpty(gdbPath))
                        results.Add($"GDB: {gdbPath}");
                }

                MessageBox.Show($"导出完成！\n\n{string.Join("\n", results)}", "成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误");
            }
        }

        private async Task<string> ExportToGdbAsync(string outputFolder, string timestamp)
        {
            string gdbName = $"压盖分析_{timestamp}.gdb";
            string gdbPath = Path.Combine(outputFolder, gdbName);
            string areaFieldName = GetAreaFieldName();

            var groupedByLayer = _intersectGeometries.GroupBy(g => g.OverlayLayerName);
            int exportedCount = 0;

            await QueuedTask.Run(() =>
            {
                var createGdbParams = Geoprocessing.MakeValueArray(outputFolder, gdbName);
                var createGdbResult = Geoprocessing.ExecuteToolAsync("management.CreateFileGDB", createGdbParams).Result;

                if (createGdbResult.IsFailed)
                    return;

                using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath))))
                {
                    foreach (var layerGroup in groupedByLayer)
                    {
                        string safeLayerName = MakeSafeFileName(layerGroup.Key);
                        string fcName = $"压盖_{safeLayerName}";

                        try
                        {
                            var fieldDescriptions = new List<ArcGIS.Core.Data.DDL.FieldDescription>
                            {
                                new ArcGIS.Core.Data.DDL.FieldDescription("GroupKey", FieldType.String) { Length = 100 },
                                new ArcGIS.Core.Data.DDL.FieldDescription("LayerName", FieldType.String) { Length = 100 },
                                new ArcGIS.Core.Data.DDL.FieldDescription(areaFieldName, FieldType.Double)
                            };

                            var shapeDescription = new ShapeDescription(GeometryType.Polygon, _spatialReference);
                            var fcDescription = new FeatureClassDescription(fcName, fieldDescriptions, shapeDescription);

                            var schemaBuilder = new SchemaBuilder(geodatabase);
                            schemaBuilder.Create(fcDescription);

                            if (!schemaBuilder.Build())
                                continue;

                            using (var fc = geodatabase.OpenDataset<FeatureClass>(fcName))
                            {
                                using (var insertCursor = fc.CreateInsertCursor())
                                {
                                    using (var buffer = fc.CreateRowBuffer())
                                    {
                                        foreach (var item in layerGroup)
                                        {
                                            buffer["GroupKey"] = item.GroupKey ?? "";
                                            buffer["LayerName"] = item.OverlayLayerName ?? "";
                                            buffer[areaFieldName] = Math.Round(ConvertAreaUnit(item.Area), _decimalPlaces);
                                            buffer[fc.GetDefinition().GetShapeField()] = item.Geometry;
                                            insertCursor.Insert(buffer);
                                        }
                                    }
                                    insertCursor.Flush();
                                }
                            }
                            exportedCount++;
                        }
                        catch { }
                    }
                }
            });

            return exportedCount > 0 ? gdbPath : null;
        }

        private string GetAreaFieldName()
        {
            return _areaUnit switch
            {
                "平方米" => "Area_M2",
                "公顷" => "Area_Ha",
                "亩" => "Area_Mu",
                _ => "Area_M2"
            };
        }

        private double ConvertAreaUnit(double areaInSquareMeters)
        {
            return _areaUnit switch
            {
                "平方米" => areaInSquareMeters,
                "公顷" => areaInSquareMeters / 10000.0,
                "亩" => areaInSquareMeters / 666.6666666667,
                _ => areaInSquareMeters
            };
        }

        private string MakeSafeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Replace(" ", "_");
        }

        private void ExportToCsv(string filePath)
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine(string.Join(",", _dataTable.Columns.Cast<DataColumn>().Select(c => $"\"{c.ColumnName}\"")));
                foreach (DataRow row in _dataTable.Rows)
                    writer.WriteLine(string.Join(",", row.ItemArray.Select(item => item is double d ? d.ToString($"F{_decimalPlaces}") : $"\"{item}\"")));
            }
        }

        /// <summary>
        /// 设置单元格值并释放 COM 对象
        /// </summary>
        private void SetCellValue(Excel.Worksheet ws, int row, int col, object value)
        {
            Excel.Range cell = null;
            try
            {
                cell = (Excel.Range)ws.Cells[row, col];
                cell.Value2 = value;
            }
            finally
            {
                if (cell != null) Marshal.ReleaseComObject(cell);
            }
        }

        /// <summary>
        /// 设置单元格值和数字格式并释放 COM 对象
        /// </summary>
        private void SetCellValueWithFormat(Excel.Worksheet ws, int row, int col, object value, string numberFormat)
        {
            Excel.Range cell = null;
            try
            {
                cell = (Excel.Range)ws.Cells[row, col];
                cell.Value2 = value;
                cell.NumberFormat = numberFormat;
            }
            finally
            {
                if (cell != null) Marshal.ReleaseComObject(cell);
            }
        }

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

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
