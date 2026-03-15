#nullable enable

using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using XIAOFUTools.Tools.InternetTileDownload;

namespace XIAOFUTools.Tools.InternetTileDownload.Infrastructure
{
    internal static class InternetWmtsCapabilitiesParser
    {
        private const double StandardPixelSize = 0.00028d;
        private const double MetersPerDegree = 111319.49079327358d;

        public static InternetTileServiceDefinition Parse(string xml, InternetTileTemplateParseResult templateInfo)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                throw new InvalidOperationException("WMTS 能力文档为空。");
            }

            var document = XDocument.Parse(xml);
            XNamespace wmts = "http://www.opengis.net/wmts/1.0";
            XNamespace ows = "http://www.opengis.net/ows/1.1";

            var contents = document.Root?.Element(wmts + "Contents")
                ?? throw new InvalidOperationException("WMTS 能力文档缺少 Contents 节点。");

            var layerIdentifier = templateInfo.LayerIdentifier
                ?? throw new InvalidOperationException("WMTS 链接缺少图层标识。");
            var matrixSetIdentifier = templateInfo.MatrixSetIdentifier;

            var layerElement = contents
                .Elements(wmts + "Layer")
                .FirstOrDefault(element =>
                    string.Equals(
                        element.Element(ows + "Identifier")?.Value,
                        layerIdentifier,
                        StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"WMTS 能力文档中未找到图层 {layerIdentifier}。");

            if (string.IsNullOrWhiteSpace(matrixSetIdentifier))
            {
                matrixSetIdentifier = layerElement
                    .Elements(wmts + "TileMatrixSetLink")
                    .Select(element => element.Element(wmts + "TileMatrixSet")?.Value)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            }

            if (string.IsNullOrWhiteSpace(matrixSetIdentifier))
            {
                throw new InvalidOperationException($"图层 {layerIdentifier} 未找到可用的 TileMatrixSet。");
            }

            var matrixSetElement = contents
                .Elements(wmts + "TileMatrixSet")
                .FirstOrDefault(element =>
                    string.Equals(
                        element.Element(ows + "Identifier")?.Value,
                        matrixSetIdentifier,
                        StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"WMTS 能力文档中未找到矩阵集 {matrixSetIdentifier}。");

            var supportedCrs = NormalizeSpatialReferenceText(matrixSetElement.Element(ows + "SupportedCRS")?.Value, templateInfo.SourceSpatialReferenceText);
            var matrixProfile = InferMatrixProfile(supportedCrs, templateInfo.MatrixProfile);
            var styleIdentifier = layerElement
                .Elements(wmts + "Style")
                .Where(element => string.Equals(element.Attribute("isDefault")?.Value, "true", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Element(ows + "Identifier")?.Value)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? templateInfo.StyleIdentifier;
            var format = layerElement
                .Elements(wmts + "Format")
                .Select(element => element.Value)
                .FirstOrDefault(value => string.Equals(value, templateInfo.Format, StringComparison.OrdinalIgnoreCase))
                ?? templateInfo.Format;

            var levels = matrixSetElement
                .Elements(wmts + "TileMatrix")
                .Select(element => CreateLevelDefinition(element, ows, supportedCrs, templateInfo.VendorKind))
                .OrderBy(level => ParseLevelOrder(level.LevelId))
                .ThenBy(level => level.LevelId, StringComparer.Ordinal)
                .ToArray();

            return new InternetTileServiceDefinition
            {
                ServiceKind = InternetTileServiceKind.Wmts,
                UrlTemplate = templateInfo.NormalizedTemplate,
                CapabilitiesUrl = templateInfo.CapabilitiesUrl,
                LayerIdentifier = layerIdentifier,
                MatrixSetIdentifier = matrixSetIdentifier,
                StyleIdentifier = styleIdentifier,
                Format = format,
                SourceSpatialReferenceText = supportedCrs,
                MatrixProfile = matrixProfile,
                TemplateMode = templateInfo.TemplateMode,
                RowOrigin = templateInfo.RowOrigin,
                VendorKind = templateInfo.VendorKind,
                Subdomains = templateInfo.Subdomains,
                Levels = levels
            };
        }

        private static InternetTileLevelDefinition CreateLevelDefinition(
            XElement matrixElement,
            XNamespace ows,
            string supportedCrs,
            InternetTileVendorKind vendorKind)
        {
            var levelId = matrixElement.Element(ows + "Identifier")?.Value
                ?? throw new InvalidOperationException("TileMatrix 缺少 Identifier。");
            var scaleDenominator = ParseDouble(matrixElement.Element(matrixElement.Name.Namespace + "ScaleDenominator")?.Value, "ScaleDenominator");
            var topLeftCornerText = matrixElement.Element(matrixElement.Name.Namespace + "TopLeftCorner")?.Value
                ?? throw new InvalidOperationException($"TileMatrix {levelId} 缺少 TopLeftCorner。");
            var topLeft = NormalizeTopLeftCorner(ParseCoordinatePair(topLeftCornerText, levelId), supportedCrs, vendorKind);
            var tileWidth = ParseInt(matrixElement.Element(matrixElement.Name.Namespace + "TileWidth")?.Value, "TileWidth");
            var tileHeight = ParseInt(matrixElement.Element(matrixElement.Name.Namespace + "TileHeight")?.Value, "TileHeight");
            var matrixWidth = ParseInt(matrixElement.Element(matrixElement.Name.Namespace + "MatrixWidth")?.Value, "MatrixWidth");
            var matrixHeight = ParseInt(matrixElement.Element(matrixElement.Name.Namespace + "MatrixHeight")?.Value, "MatrixHeight");
            var resolution = NormalizeResolution(scaleDenominator, supportedCrs, vendorKind, topLeft, tileWidth, matrixWidth);

            return new InternetTileLevelDefinition
            {
                LevelId = levelId,
                Resolution = resolution,
                TopLeftX = topLeft.x,
                TopLeftY = topLeft.y,
                TileWidth = tileWidth,
                TileHeight = tileHeight,
                MatrixWidth = matrixWidth,
                MatrixHeight = matrixHeight
            };
        }

        private static double ConvertScaleDenominatorToResolution(double scaleDenominator, string supportedCrs)
        {
            var metersPerUnit = string.Equals(supportedCrs, "EPSG:4326", StringComparison.OrdinalIgnoreCase)
                ? MetersPerDegree
                : 1d;
            return (scaleDenominator * StandardPixelSize) / metersPerUnit;
        }

        private static double NormalizeResolution(
            double scaleDenominator,
            string supportedCrs,
            InternetTileVendorKind vendorKind,
            (double x, double y) topLeft,
            int tileWidth,
            int matrixWidth)
        {
            if (vendorKind == InternetTileVendorKind.Tianditu && tileWidth > 0 && matrixWidth > 0)
            {
                var fullWidth = Math.Abs(topLeft.x) * 2d;
                if (fullWidth > 0)
                {
                    return fullWidth / (matrixWidth * tileWidth);
                }
            }

            return ConvertScaleDenominatorToResolution(scaleDenominator, supportedCrs);
        }

        private static (double x, double y) NormalizeTopLeftCorner(
            (double x, double y) topLeft,
            string supportedCrs,
            InternetTileVendorKind vendorKind)
        {
            if (vendorKind != InternetTileVendorKind.Tianditu)
            {
                return topLeft;
            }

            if ((string.Equals(supportedCrs, "EPSG:3857", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(supportedCrs, "EPSG:4326", StringComparison.OrdinalIgnoreCase)) &&
                topLeft.x > 0 &&
                topLeft.y < 0)
            {
                return (topLeft.y, topLeft.x);
            }

            return topLeft;
        }

        private static string NormalizeSpatialReferenceText(string? supportedCrsText, string fallback)
        {
            if (string.IsNullOrWhiteSpace(supportedCrsText))
            {
                return fallback;
            }

            var trimmed = supportedCrsText.Trim();
            if (trimmed.Contains("3857", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("900913", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("102100", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("102113", StringComparison.OrdinalIgnoreCase))
            {
                return "EPSG:3857";
            }

            if (trimmed.Contains("4326", StringComparison.OrdinalIgnoreCase))
            {
                return "EPSG:4326";
            }

            return trimmed;
        }

        private static InternetTileMatrixProfile InferMatrixProfile(string spatialReferenceText, InternetTileMatrixProfile fallback)
        {
            if (string.Equals(spatialReferenceText, "EPSG:3857", StringComparison.OrdinalIgnoreCase))
            {
                return InternetTileMatrixProfile.WebMercator;
            }

            if (string.Equals(spatialReferenceText, "EPSG:4326", StringComparison.OrdinalIgnoreCase))
            {
                return InternetTileMatrixProfile.Geographic;
            }

            return fallback;
        }

        private static (double x, double y) ParseCoordinatePair(string text, string levelId)
        {
            var parts = text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                throw new InvalidOperationException($"TileMatrix {levelId} 的 TopLeftCorner 无法解析。");
            }

            return (
                ParseDouble(parts[0], "TopLeftCorner.X"),
                ParseDouble(parts[1], "TopLeftCorner.Y"));
        }

        private static double ParseDouble(string? value, string fieldName)
        {
            if (!double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new InvalidOperationException($"无法解析 WMTS 字段 {fieldName}。");
            }

            return parsed;
        }

        private static int ParseInt(string? value, string fieldName)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new InvalidOperationException($"无法解析 WMTS 字段 {fieldName}。");
            }

            return parsed;
        }

        private static int ParseLevelOrder(string levelId)
        {
            return int.TryParse(levelId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : int.MaxValue;
        }
    }
}
