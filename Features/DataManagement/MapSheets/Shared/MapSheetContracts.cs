using System;
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Shared
{
    public readonly record struct MapSheetCell(string Code, double XMin, double XMax, double YMin, double YMax)
    {
        public double Width => XMax - XMin;
        public double Height => YMax - YMin;
    }

    public readonly record struct LargeScaleMapSheetOption(string Name, double Width, double Height);

    public readonly record struct SmallScaleMapSheetOption(string Name, string ScaleCode, int Rows, int Columns);

    public sealed class MapSheetIdentifyResult
    {
        public static MapSheetIdentifyResult Empty { get; } =
            new(Array.Empty<string>(), Array.Empty<MapSheetCell>());

        public MapSheetIdentifyResult(IEnumerable<string> codes, IEnumerable<MapSheetCell> cells)
        {
            Codes = (codes ?? Array.Empty<string>())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();

            Cells = (cells ?? Array.Empty<MapSheetCell>())
                .GroupBy(cell => cell.Code, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(cell => cell.Code, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<string> Codes { get; }

        public IReadOnlyList<MapSheetCell> Cells { get; }

        public bool HasResult => Codes.Count > 0;

        public string DisplayText => HasResult ? string.Join("、", Codes) : "未命中图幅";

        public string GetOverlayText(int maxLines = 4)
        {
            if (!HasResult)
            {
                return "未命中图幅";
            }

            var effectiveLines = Math.Max(1, maxLines);
            var visibleCodes = Codes.Take(effectiveLines).ToArray();
            var text = string.Join(Environment.NewLine, visibleCodes);

            return Codes.Count > effectiveLines
                ? $"{text}{Environment.NewLine}..."
                : text;
        }
    }

    internal sealed class FieldOption
    {
        public FieldOption(string name, string alias, int length)
        {
            Name = name;
            Alias = alias;
            Length = length;
        }

        public string Name { get; }

        public string Alias { get; }

        public int Length { get; }

        public string DisplayName => string.IsNullOrWhiteSpace(Alias) || string.Equals(Name, Alias, StringComparison.Ordinal)
            ? Name
            : $"{Name}({Alias})";

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
