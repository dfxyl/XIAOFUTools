using System;
using System.IO;
using System.Windows;
using XIAOFUTools.Features.Analysis.IntersectSummary.Infrastructure;

namespace XIAOFUTools.Features.Analysis.IntersectSummary
{
    public partial class IntersectSummaryResultWindow
    {
        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            var filePath = PresentationServices.Files.SaveFile(
                "Excel文件 (*.xlsx)|*.xlsx|CSV文件 (*.csv)|*.csv",
                $"交集汇总表_{DateTime.Now:yyyyMMdd_HHmmss}",
                defaultExtension: ".xlsx");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                if (Path.GetExtension(filePath).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    IntersectSummaryCsvExporter.Export(_dataTable, filePath, _decimalPlaces);
                }
                else
                {
                    IntersectSummaryExcelExporter.Export(
                        _dataTable,
                        filePath,
                        _decimalPlaces,
                        _regionFieldCount);
                }

                PresentationServices.Dialogs.Show($"导出成功!\n{filePath}", "成功");
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show($"导出失败: {ex.Message}", "错误");
            }
        }
    }
}
