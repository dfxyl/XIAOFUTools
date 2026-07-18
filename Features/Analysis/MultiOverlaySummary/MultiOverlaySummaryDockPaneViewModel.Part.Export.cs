using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using XIAOFUTools.Features.Analysis.MultiOverlaySummary.Infrastructure;

namespace XIAOFUTools.Features.Analysis.MultiOverlaySummary
{
    internal partial class MultiOverlaySummaryDockPaneViewModel
    {
        private readonly IMultiOverlayExcelExporter _excelExporter = new MultiOverlayExcelExporter();

        private void CreateResultTable(Dictionary<string, Dictionary<string, double>> results,
            Dictionary<string, double> mainFeatureAreas, List<string> overlayLayerNames, string uniqueFieldName)
        {
            var table = new DataTable();

            // 唯一字段列名
            string keyColumnName = string.IsNullOrEmpty(uniqueFieldName) ? "分组"
                : (SelectedUniqueField?.Alias ?? uniqueFieldName);
            table.Columns.Add(keyColumnName, typeof(string));

            // 主图层面积列 - 使用主图层名称
            string mainAreaColumnName = $"{_mainLayerName}({SelectedAreaUnit})";
            table.Columns.Add(mainAreaColumnName, typeof(double));

            // 压盖图层面积列
            foreach (var layerName in overlayLayerNames)
                table.Columns.Add($"{layerName}({SelectedAreaUnit})", typeof(double));

            foreach (var kvp in results.OrderBy(x => x.Key))
            {
                var row = table.NewRow();
                row[0] = kvp.Key;
                row[1] = ConvertAreaUnit(mainFeatureAreas[kvp.Key], SelectedAreaUnit);

                int colIndex = 2;
                foreach (var layerName in overlayLayerNames)
                {
                    double area = kvp.Value.ContainsKey(layerName) ? kvp.Value[layerName] : 0;
                    row[colIndex++] = ConvertAreaUnit(area, SelectedAreaUnit);
                }
                table.Rows.Add(row);
            }

            // 合计行
            var totalRow = table.NewRow();
            totalRow[0] = "合计";
            totalRow[1] = Math.Round(table.AsEnumerable().Sum(r => Convert.ToDouble(r[1])), DecimalPlaces);
            for (int i = 2; i < table.Columns.Count; i++)
                totalRow[i] = Math.Round(table.AsEnumerable().Sum(r => Convert.ToDouble(r[i])), DecimalPlaces);
            table.Rows.Add(totalRow);

            // 应用小数位数
            foreach (DataRow row in table.Rows)
            {
                for (int i = 1; i < table.Columns.Count; i++)
                {
                    if (row[i] is double d)
                        row[i] = Math.Round(d, DecimalPlaces);
                }
            }

            PresentationServices.UiThread.Invoke(() =>
            {
                ResultTable = table;
                NotifyPropertyChanged(() => HasResult);
            });
        }

        private async Task ShowExportOptionsAsync()
        {
            if (ResultTable == null || ResultTable.Rows.Count == 0)
            {
                PresentationServices.Dialogs.Show("没有可导出的数据", "提示");
                return;
            }

            var exportOptions = _dialogService.SelectExportOptions();
            if (exportOptions is null)
                return;

            // 选择输出文件夹
            var outputFolder = PresentationServices.Files.SelectFolder("选择导出文件夹");
            if (string.IsNullOrWhiteSpace(outputFolder))
                return;

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var results = new List<string>();

            try
            {
                IsProcessing = true;

                // 导出Excel
                if (exportOptions.ExportExcel)
                {
                    string excelPath = Path.Combine(outputFolder, $"多图层压盖汇总表_{timestamp}.xlsx");
                    await _excelExporter.ExportAsync(
                        ResultTable,
                        DecimalPlaces,
                        excelPath,
                        CancellationToken.None);
                    results.Add($"表格: {excelPath}");
                    LogInfo($"已导出表格: {excelPath}");
                }

                // 导出GDB
                if (exportOptions.ExportGdb && _intersectGeometries?.Count > 0)
                {
                    string gdbPath = await ExportToGdbAsync(outputFolder, timestamp);
                    if (!string.IsNullOrEmpty(gdbPath))
                        results.Add($"GDB: {gdbPath}");
                }

                StatusMessage = "导出完成！";
                PresentationServices.Dialogs.Show($"导出完成！\n\n{string.Join("\n", results)}", "成功");
            }
            catch (Exception ex)
            {
                LogError($"导出失败: {ex.Message}");
                PresentationServices.Dialogs.Show($"导出失败: {ex.Message}", "错误");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task<string> ExportToGdbAsync(string outputFolder, string timestamp)
        {
            if (_intersectGeometries == null || _intersectGeometries.Count == 0)
                return null;

            string gdbName = $"压盖分析_{timestamp}.gdb";
            string gdbPath = Path.Combine(outputFolder, gdbName);

            StatusMessage = "正在导出GDB...";
            LogInfo($"开始导出GDB到: {gdbPath}");

            var groupedByLayer = _intersectGeometries.GroupBy(g => g.OverlayLayerName);
            int exportedCount = 0;
            string areaFieldName = GetAreaFieldName();

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
                        string layerName = layerGroup.Key;
                        string safeLayerName = MakeSafeFileName(layerName);
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
                            {
                                LogError($"创建要素类失败: {fcName}");
                                continue;
                            }

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
                                            // 根据用户设置的单位转换面积
                                            buffer[areaFieldName] = Math.Round(ConvertAreaUnit(item.Area, SelectedAreaUnit), DecimalPlaces);
                                            buffer[fc.GetDefinition().GetShapeField()] = item.Geometry;
                                            insertCursor.Insert(buffer);
                                        }
                                    }
                                    insertCursor.Flush();
                                }
                            }

                            LogInfo($"已导出: {fcName} ({layerGroup.Count()} 个要素)");
                            exportedCount++;
                        }
                        catch (Exception ex)
                        {
                            LogError($"导出 {layerName} 失败: {ex.Message}");
                        }
                    }
                }
            });

            LogInfo($"GDB导出完成，共导出 {exportedCount} 个图层");
            return exportedCount > 0 ? gdbPath : null;
        }

        private void ExportToCsv(string filePath)
        {
            using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                var columnNames = ResultTable.Columns.Cast<DataColumn>().Select(c => $"\"{c.ColumnName}\"");
                writer.WriteLine(string.Join(",", columnNames));

                foreach (DataRow row in ResultTable.Rows)
                {
                    var values = row.ItemArray.Select(item =>
                        item is double d ? d.ToString($"F{DecimalPlaces}") : $"\"{item}\"");
                    writer.WriteLine(string.Join(",", values));
                }
            }
        }
    }
}
