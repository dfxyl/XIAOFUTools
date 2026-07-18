using System;
using System.Threading.Tasks;
using System.Windows;
using XIAOFUTools.Features.Custom.DevZoneCheck.Infrastructure;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Custom.DevZoneCheck
{
    internal partial class DevZoneCheckDockPaneViewModel
    {
        private string FormatArea(double area) => Math.Round(area, DecimalPlaces).ToString($"F{DecimalPlaces}");

        private async Task ExportReportAsync()
        {
            if (_checkResults.Count == 0)
            {
                PresentationServices.Dialogs.Show("没有可导出的结果！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var reportPath = PresentationServices.Files.SaveFile(
                "Excel文件|*.xlsx", $"开发区整合优化核查报告_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                title: "导出核查报告", defaultExtension: ".xlsx");
            if (string.IsNullOrWhiteSpace(reportPath)) return;

            try
            {
                IsProcessing = true; LogInfo("正在导出报告...");
                await DevZoneExcelReportExporter.ExportAsync(_checkResults, new DevZoneReportOptions(
                    SelectedAreaUnit, DecimalPlaces, EnableUrbanBoundary, EnablePermanentFarmland,
                    EnableEcoRedline, EnableLandSurvey, EnableApprovedLand, EnableApprovedNotSupplied,
                    EnableSuppliedLand, EnableIdleLand, EnableSpatialPlanning, EnableEvidenceData,
                    EnableSupplyYearData), reportPath);
                LogInfo($"报告已导出: {reportPath}");
                PresentationServices.Dialogs.Show($"报告已导出到:\n{reportPath}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogError($"导出失败: {ex.Message}");
                PresentationServices.Dialogs.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsProcessing = false; }
        }
    }
}
