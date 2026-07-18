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

    private static void BuildResultSheet(object sheetObject, JObject payload)
    {
        ((dynamic)sheetObject).Cells[1, 1] = "字段";
        ((dynamic)sheetObject).Cells[1, 2] = "值";
        AIAssistantDockPaneView.ApplyHeaderStyle(((dynamic)sheetObject).Range["A1", "B1"]);
        List<(string, string)> resultRows = new List<(string, string)>();
        JArray tableRows = null;
        if (TryParseJsonToken(payload["result"], out var resultToken) && resultToken is JObject resultObj)
        {
            resultRows.Add(("success", resultObj["success"]?.ToString() ?? string.Empty));
            resultRows.Add(("message", resultObj["message"]?.ToString() ?? string.Empty));
            resultRows.Add(("error", resultObj["error"]?.ToString() ?? string.Empty));
            if (resultObj["data"] is JObject data)
            {
                if (data["stats"] is JObject stats)
                {
                    foreach (JProperty property in stats.Properties())
                    {
                        resultRows.Add(("stats." + property.Name, property.Value?.ToString() ?? string.Empty));
                    }
                }

                if (data["warnings"] is JArray { Count: > 0 } warnings)
                {
                    for (int i = 0; i < warnings.Count; i++)
                    {
                        resultRows.Add(($"warnings[{i}]", warnings[i]?.ToString() ?? string.Empty));
                    }
                }

                tableRows = data["rows"] as JArray;
            }
        }
        else
        {
            resultRows.Add(("preview", payload["preview"]?.ToString() ?? string.Empty));
        }

        int rowIndex = 2;
        foreach (var row in resultRows.Where<(string, string)>(((string Key, string Value) r) => !string.IsNullOrWhiteSpace(r.Value)))
        {
            ((dynamic)sheetObject).Cells[rowIndex, 1] = row.Item1;
            ((dynamic)sheetObject).Cells[rowIndex, 2] = row.Item2;
            rowIndex++;
        }

        if (tableRows != null && tableRows.Count > 0)
        {
            rowIndex += 2;
            ((dynamic)sheetObject).Cells[rowIndex, 1] = "明细数据";
            ((dynamic)sheetObject).Cells[rowIndex, 1].Font.Bold = true;
            rowIndex++;
            List<string> headers = CollectTableHeaders(tableRows);
            for (int c = 0; c < headers.Count; c++)
            {
                ((dynamic)sheetObject).Cells[rowIndex, c + 1] = headers[c];
            }

            AIAssistantDockPaneView.ApplyHeaderStyle(((dynamic)sheetObject).Range[((dynamic)sheetObject).Cells[rowIndex, 1], ((dynamic)sheetObject).Cells[rowIndex, headers.Count]]);
            rowIndex++;
            foreach (JObject rowToken in tableRows.OfType<JObject>())
            {
                for (int c2 = 0; c2 < headers.Count; c2++)
                {
                    JToken value = rowToken[headers[c2]];
                    ((dynamic)sheetObject).Cells[rowIndex, c2 + 1] = value?.ToString() ?? string.Empty;
                }

                rowIndex++;
            }
        }

        ((dynamic)sheetObject).Columns[1].ColumnWidth = 30;
        ((dynamic)sheetObject).Columns[2].ColumnWidth = 90;
        ((dynamic)sheetObject).Columns[2].WrapText = true;
        ((dynamic)sheetObject).UsedRange.Columns.AutoFit();
    }

    private static void ApplyHeaderStyle(object rangeObject)
    {
        if (rangeObject != null)
        {
            ((dynamic)rangeObject).Font.Bold = true;
            ((dynamic)rangeObject).Interior.Color = ColorTranslator.ToOle(Color.FromArgb(229, 231, 235));
            ((dynamic)rangeObject).Borders.LineStyle = 1;
        }
    }
    }
}
