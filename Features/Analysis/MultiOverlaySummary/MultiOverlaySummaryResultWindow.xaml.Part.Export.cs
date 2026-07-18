using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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

            var createGdbParams = Geoprocessing.MakeValueArray(outputFolder, gdbName);
            var createGdbResult = await Geoprocessing.ExecuteToolAsync(
                "management.CreateFileGDB",
                createGdbParams,
                null,
                CancellationToken.None,
                null,
                GPExecuteToolFlags.GPThread);
            if (createGdbResult.IsFailed)
            {
                throw new InvalidOperationException("创建GDB失败: " + string.Join("; ", createGdbResult.Messages.Select(message => message.Text)));
            }

            await QueuedTask.Run(() =>
            {
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
    }
}
