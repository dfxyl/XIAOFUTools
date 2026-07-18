#nullable enable

using System;
using System.Linq;

namespace XIAOFUTools.Features.General.InternetTileDownload.Services
{
    internal static class InternetTileRequestExpander
    {
        public static string Expand(InternetTileServiceDefinition definition, string levelId, int row, int column)
        {
            ArgumentNullException.ThrowIfNull(definition);

            var requestRow = definition.RowOrigin == InternetTileRowOrigin.Bottom
                ? InvertRow(definition, levelId, row)
                : row;

            var template = definition.UrlTemplate;
            if (definition.Subdomains.Count > 0)
            {
                var subdomainIndex = Math.Abs(row + column) % definition.Subdomains.Count;
                template = template.Replace("{subdomain}", definition.Subdomains[subdomainIndex], StringComparison.OrdinalIgnoreCase);
            }

            if (definition.ServiceKind == InternetTileServiceKind.QuadKey ||
                definition.TemplateMode == InternetTileTemplateMode.QuadKey)
            {
                var quadKey = BuildQuadKey(levelId, requestRow, column);
                template = template.Replace("{quadkey}", quadKey, StringComparison.OrdinalIgnoreCase);
            }

            return template
                .Replace("{level}", Uri.EscapeDataString(levelId), StringComparison.Ordinal)
                .Replace("{row}", requestRow.ToString(), StringComparison.Ordinal)
                .Replace("{col}", column.ToString(), StringComparison.Ordinal);
        }

        private static int InvertRow(InternetTileServiceDefinition definition, string levelId, int row)
        {
            var level = definition.Levels.FirstOrDefault(item => string.Equals(item.LevelId, levelId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"未找到级别 {levelId} 的矩阵定义。");
            return level.MatrixHeight - 1 - row;
        }

        private static string BuildQuadKey(string levelId, int row, int column)
        {
            if (!int.TryParse(levelId, out var zoomLevel) || zoomLevel < 0)
            {
                throw new InvalidOperationException("QuadKey 模式要求级别标识为非负整数。");
            }

            var chars = new char[zoomLevel];
            for (var level = zoomLevel; level > 0; level--)
            {
                var digit = '0';
                var mask = 1 << (level - 1);
                if ((column & mask) != 0)
                {
                    digit++;
                }

                if ((row & mask) != 0)
                {
                    digit += (char)2;
                }

                chars[zoomLevel - level] = digit;
            }

            return new string(chars);
        }
    }
}
