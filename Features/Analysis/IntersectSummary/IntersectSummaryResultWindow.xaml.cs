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
    }
}
