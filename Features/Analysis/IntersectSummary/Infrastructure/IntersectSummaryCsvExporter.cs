using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace XIAOFUTools.Features.Analysis.IntersectSummary.Infrastructure
{
    internal static class IntersectSummaryCsvExporter
    {
        internal static void Export(DataTable table, string filePath, int decimalPlaces)
        {
            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
            writer.WriteLine(string.Join(",", table.Columns
                .Cast<DataColumn>()
                .Select(column => Escape(column.ColumnName))));

            foreach (DataRow row in table.Rows)
            {
                var values = new List<string>(row.ItemArray.Length);
                foreach (var value in row.ItemArray)
                {
                    values.Add(value is double number
                        ? number.ToString($"F{decimalPlaces}")
                        : Escape(value?.ToString() ?? string.Empty));
                }

                writer.WriteLine(string.Join(",", values));
            }
        }

        private static string Escape(string value)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
