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
        /// 全选
        /// </summary>
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            ResultDataGrid.SelectAll();
        }
    }
}
