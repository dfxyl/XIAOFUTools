using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace XIAOFUTools.Features.Analysis.IntersectSummary.Core
{
    internal static class IntersectSummaryTableBuilder
    {
        internal static DataTable Build(
            IReadOnlyList<IntersectSummaryResultItem> results,
            IReadOnlyList<string> regionFields,
            IReadOnlyList<string> classFields,
            IReadOnlyDictionary<string, string> regionColumnNames,
            IReadOnlyDictionary<string, string> classColumnNames,
            string areaUnit,
            int decimalPlaces)
        {
            var table = new DataTable();
            foreach (var field in regionFields)
            {
                table.Columns.Add(ResolveColumnName(field, regionColumnNames), typeof(string));
            }

            foreach (var field in classFields)
            {
                table.Columns.Add(ResolveColumnName(field, classColumnNames), typeof(string));
            }

            table.Columns.Add($"面积({areaUnit})", typeof(double));
            var adjustedAreas = ApplyLargestRemainderMethod(
                results,
                regionFields,
                areaUnit,
                decimalPlaces);

            for (var index = 0; index < results.Count; index++)
            {
                var result = results[index];
                var row = table.NewRow();
                var columnIndex = 0;
                foreach (var field in regionFields)
                {
                    row[columnIndex++] = ReadValue(result.RegionValues, field);
                }

                foreach (var field in classFields)
                {
                    row[columnIndex++] = ReadValue(result.ClassValues, field);
                }

                row[columnIndex] = adjustedAreas[index];
                table.Rows.Add(row);
            }

            AddTotalRow(table, regionFields.Count, classFields.Count, adjustedAreas, decimalPlaces);
            return table;
        }

        internal static IReadOnlyList<double> ApplyLargestRemainderMethod(
            IReadOnlyList<IntersectSummaryResultItem> results,
            IReadOnlyList<string> regionFields,
            string areaUnit,
            int decimalPlaces)
        {
            var adjustedAreas = new double[results.Count];
            var minUnit = Math.Pow(10, -decimalPlaces);
            var groups = results
                .Select((result, index) => new { Result = result, Index = index })
                .GroupBy(item => string.Join("|", regionFields.Select(field => ReadValue(item.Result.RegionValues, field))));

            foreach (var group in groups)
            {
                var items = group.ToList();
                var convertedAreas = items
                    .Select(item => ConvertAreaUnit(item.Result.AdjustedArea, areaUnit))
                    .ToList();
                var groupTotal = Math.Round(convertedAreas.Sum(), decimalPlaces);
                var floorValues = convertedAreas
                    .Select(area => Math.Floor(area / minUnit) * minUnit)
                    .ToList();
                var unitsToDistribute = (int)Math.Round(
                    (groupTotal - Math.Round(floorValues.Sum(), decimalPlaces)) / minUnit);
                var remainderOrder = convertedAreas
                    .Select((area, index) => new
                    {
                        Index = index,
                        Remainder = area - floorValues[index]
                    })
                    .OrderByDescending(item => item.Remainder)
                    .ThenBy(item => item.Index)
                    .ToList();

                for (var index = 0; index < Math.Min(unitsToDistribute, remainderOrder.Count); index++)
                {
                    floorValues[remainderOrder[index].Index] += minUnit;
                }

                for (var index = 0; index < items.Count; index++)
                {
                    adjustedAreas[items[index].Index] = Math.Round(floorValues[index], decimalPlaces);
                }
            }

            return adjustedAreas;
        }

        private static void AddTotalRow(
            DataTable table,
            int regionFieldCount,
            int classFieldCount,
            IReadOnlyCollection<double> adjustedAreas,
            int decimalPlaces)
        {
            var totalRow = table.NewRow();
            var columnIndex = 0;
            if (regionFieldCount > 0)
            {
                totalRow[columnIndex++] = "合计";
                for (var index = 1; index < regionFieldCount; index++)
                {
                    totalRow[columnIndex++] = string.Empty;
                }
            }

            for (var index = 0; index < classFieldCount; index++)
            {
                totalRow[columnIndex++] = regionFieldCount == 0 && index == 0 ? "合计" : string.Empty;
            }

            totalRow[columnIndex] = Math.Round(adjustedAreas.Sum(), decimalPlaces);
            table.Rows.Add(totalRow);
        }

        private static string ResolveColumnName(
            string field,
            IReadOnlyDictionary<string, string> columnNames)
        {
            return columnNames.TryGetValue(field, out var columnName) && !string.IsNullOrWhiteSpace(columnName)
                ? columnName
                : field;
        }

        private static string ReadValue(IReadOnlyDictionary<string, object> values, string field)
        {
            return values.TryGetValue(field, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        }

        private static double ConvertAreaUnit(double squareMeters, string targetUnit)
        {
            return targetUnit switch
            {
                "公顷" => squareMeters / 10000.0,
                "亩" => squareMeters / 666.6666666667,
                _ => squareMeters
            };
        }
    }
}
