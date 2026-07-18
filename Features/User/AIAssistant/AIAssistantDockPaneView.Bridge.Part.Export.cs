#define DEBUG
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Dialogs;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Features.User.AIAssistant.Agent;
using XIAOFUTools.Features.User.AIAssistant.Application;
using XIAOFUTools.Features.User.AIAssistant.Database;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace XIAOFUTools.Features.User.AIAssistant
{
    public partial class AIAssistantDockPaneView
    {

    private async Task HandleExportToolCall(JObject payload)
    {
        if (payload == null)
        {
            await SendMessageToWebView(new { type = "toolExportFailed", message = "导出失败：未接收到工具数据。" });
            return;
        }

        try
        {
            string toolName = payload["toolName"]?.ToString();
            if (string.IsNullOrWhiteSpace(toolName))
            {
                toolName = "tool";
            }

            string defaultName = $"{SanitizeFileName(toolName)}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                AddExtension = true,
                FileName = defaultName,
                OverwritePrompt = true
            };
            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            ExportToolCallToExcel(saveDialog.FileName, payload);
            await SendMessageToWebView(new { type = "toolExported", filePath = saveDialog.FileName, fileName = Path.GetFileName(saveDialog.FileName) });
        }
        catch (Exception ex)
        {
            Debug.WriteLine("导出工具结果失败: " + ex.Message);
            await SendMessageToWebView(new { type = "toolExportFailed", message = "导出失败: " + ex.Message });
        }
    }

    private static void ExportToolCallToExcel(string filePath, JObject payload)
    {
        dynamic excelApp = null;
        dynamic workbook = null;
        object summarySheet = null;
        object parameterSheet = null;
        object resultSheet = null;
        try
        {
            Type excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null)
            {
                throw new InvalidOperationException("未检测到 Excel 组件，请先安装 Microsoft Excel。");
            }

            dynamic excel = Activator.CreateInstance(excelType);
            excel.Visible = false;
            excel.DisplayAlerts = false;
            excelApp = excel;
            dynamic wb = excel.Workbooks.Add();
            workbook = wb;
            dynamic ws1 = wb.Worksheets[1];
            ws1.Name = "概览";
            summarySheet = ws1;
            dynamic ws2 = wb.Worksheets.Add(After: ws1);
            ws2.Name = "执行参数";
            parameterSheet = ws2;
            dynamic ws3 = wb.Worksheets.Add(After: ws2);
            ws3.Name = "执行结果";
            resultSheet = ws3;
            BuildSummarySheet(summarySheet, payload);
            BuildParameterSheet(parameterSheet, payload);
            BuildResultSheet(resultSheet, payload);
            wb.SaveAs(filePath, 51);
        }
        finally
        {
            if ((object)workbook != null)
            {
                try
                {
                    workbook.Close(false);
                }
                catch
                {
                }
            }

            if ((object)excelApp != null)
            {
                try
                {
                    excelApp.Quit();
                }
                catch
                {
                }
            }

            ReleaseComObject(resultSheet);
            ReleaseComObject(parameterSheet);
            ReleaseComObject(summarySheet);
            ReleaseComObject((object)workbook);
            ReleaseComObject((object)excelApp);
        }
    }

    private static void BuildSummarySheet(object sheetObject, JObject payload)
    {
        List<(string, string)> rows = new List<(string, string)>
        {
            ("工具名称", payload["toolName"]?.ToString() ?? string.Empty),
            ("调用ID", payload["callId"]?.ToString() ?? string.Empty),
            ("状态", payload["status"]?.ToString() ?? string.Empty),
            ("执行信息", payload["message"]?.ToString() ?? string.Empty),
            ("导出时间", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        };
        if (TryParseJsonToken(payload["result"], out var resultToken) && resultToken is JObject resultObj && resultObj["data"] is JObject data)
        {
            rows.Add(("地图", data["mapName"]?.ToString() ?? string.Empty));
            rows.Add(("输入图层", data["inputLayer"]?.ToString() ?? data["targetLayer"]?.ToString() ?? string.Empty));
            rows.Add(("输出", data["outputFeatureClass"]?.ToString() ?? string.Empty));
        }

        ((dynamic)sheetObject).Cells[1, 1] = "工具执行导出";
        dynamic titleRange = ((dynamic)sheetObject).Range["A1", "B1"];
        titleRange.Merge();
        titleRange.Font.Bold = true;
        titleRange.Font.Size = 14;
        ((dynamic)sheetObject).Cells[3, 1] = "字段";
        ((dynamic)sheetObject).Cells[3, 2] = "值";
        AIAssistantDockPaneView.ApplyHeaderStyle(((dynamic)sheetObject).Range["A3", "B3"]);
        int rowIndex = 4;
        foreach (var row in rows.Where<(string, string)>(((string Key, string Value) r) => !string.IsNullOrWhiteSpace(r.Value)))
        {
            ((dynamic)sheetObject).Cells[rowIndex, 1] = row.Item1;
            ((dynamic)sheetObject).Cells[rowIndex, 2] = row.Item2;
            rowIndex++;
        }

        ((dynamic)sheetObject).Columns[1].ColumnWidth = 20;
        ((dynamic)sheetObject).Columns[2].ColumnWidth = 90;
        ((dynamic)sheetObject).Columns[2].WrapText = true;
    }

    private static void BuildParameterSheet(object sheetObject, JObject payload)
    {
        ((dynamic)sheetObject).Cells[1, 1] = "参数路径";
        ((dynamic)sheetObject).Cells[1, 2] = "参数值";
        AIAssistantDockPaneView.ApplyHeaderStyle(((dynamic)sheetObject).Range["A1", "B1"]);
        if (!TryParseJsonToken(payload["parameters"], out var parameterToken))
        {
            ((dynamic)sheetObject).Cells[2, 1] = "(无参数)";
            ((dynamic)sheetObject).Columns[1].AutoFit();
            ((dynamic)sheetObject).Columns[2].ColumnWidth = 80;
            ((dynamic)sheetObject).Columns[2].WrapText = true;
            return;
        }

        List<(string, string)> flattenRows = new List<(string, string)>();
        FlattenToken(parameterToken, string.Empty, flattenRows);
        if (flattenRows.Count == 0)
        {
            flattenRows.Add(("(空)", string.Empty));
        }

        int rowIndex = 2;
        foreach (var row in flattenRows)
        {
            ((dynamic)sheetObject).Cells[rowIndex, 1] = row.Item1;
            ((dynamic)sheetObject).Cells[rowIndex, 2] = row.Item2;
            rowIndex++;
        }

        ((dynamic)sheetObject).Columns[1].ColumnWidth = 40;
        ((dynamic)sheetObject).Columns[2].ColumnWidth = 80;
        ((dynamic)sheetObject).Columns[2].WrapText = true;
    }
    }
}
