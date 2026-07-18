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

        private string MakeSafeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Replace(" ", "_");
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

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
