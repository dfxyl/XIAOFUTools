using System;
using System.Data;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Features.Analysis.IntersectSummary
{
    public partial class IntersectSummaryResultWindow
    {

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
        /// 关闭窗口
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
