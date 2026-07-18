using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using XIAOFUTools.Features.Analysis.IntersectSummary.Core;
using XIAOFUTools.Features.Analysis.IntersectSummary.Infrastructure;

namespace XIAOFUTools.Features.Analysis.IntersectSummary
{
    internal partial class IntersectSummaryDockPaneViewModel
    {
        private void BuildResultTable(
            IReadOnlyList<IntersectSummaryResultItem> results,
            IReadOnlyList<string> regionFields,
            IReadOnlyList<string> classFields)
        {
            _regionFieldCount = regionFields.Count;
            var regionColumnNames = BuildColumnNameMap(RegionFields, regionFields);
            var classColumnNames = BuildColumnNameMap(ClassFields, classFields);
            var table = IntersectSummaryTableBuilder.Build(
                results,
                regionFields,
                classFields,
                regionColumnNames,
                classColumnNames,
                SelectedAreaUnit,
                DecimalPlaces);

            void ApplyTable()
            {
                ResultTable = table;
                NotifyPropertyChanged(() => HasResult);
            }

            PresentationServices.UiThread.InvokeOrRun(ApplyTable);
        }

        private void ExportResult()
        {
            if (ResultTable == null || ResultTable.Rows.Count == 0)
            {
                PresentationServices.Dialogs.Show("没有可导出的数据", "提示");
                return;
            }

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
                    IntersectSummaryCsvExporter.Export(ResultTable, filePath, DecimalPlaces);
                }
                else
                {
                    IntersectSummaryExcelExporter.Export(ResultTable, filePath, DecimalPlaces, _regionFieldCount);
                }

                LogInfo($"导出成功: {filePath}");
                PresentationServices.Dialogs.Show($"导出成功!\n{filePath}", "成功");
            }
            catch (Exception ex)
            {
                LogError($"导出失败: {ex.Message}");
                PresentationServices.Dialogs.Show($"导出失败: {ex.Message}", "错误");
            }
        }

        private static IReadOnlyDictionary<string, string> BuildColumnNameMap(
            IEnumerable<FieldSelectItem> fields,
            IEnumerable<string> selectedFields)
        {
            var selected = selectedFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return fields
                .Where(field => selected.Contains(field.FieldName))
                .ToDictionary(
                    field => field.FieldName,
                    field => string.IsNullOrWhiteSpace(field.Alias) || field.Alias == field.FieldName
                        ? field.FieldName
                        : field.Alias,
                    StringComparer.OrdinalIgnoreCase);
        }
    }
}
