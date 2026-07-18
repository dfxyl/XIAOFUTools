#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using XIAOFUTools.Features.General.InternetTileDownload;

namespace XIAOFUTools.Features.General.InternetTileDownload.Infrastructure
{
    internal static class InternetTileTemplateParser
    {
        private static readonly Dictionary<string, string> PlaceholderAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["{x}"] = "{col}",
            ["{tilecol}"] = "{col}",
            ["{col}"] = "{col}",
            ["{y}"] = "{row}",
            ["{tilerow}"] = "{row}",
            ["{row}"] = "{row}",
            ["{tilecol}"] = "{col}",
            ["{z}"] = "{level}",
            ["{zoom}"] = "{level}",
            ["{tilematrix}"] = "{level}",
            ["{level}"] = "{level}"
        };

        private static readonly Regex VersionSegmentRegex = new("^\\d+\\.\\d+\\.\\d+$", RegexOptions.Compiled);

        public static InternetTileTemplateParseResult Parse(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("请输入有效的互联网切片服务链接。", nameof(url));
            }

            var vendorNormalization = InternetTileVendorRules.Normalize(url);

            var uriParseCandidate = vendorNormalization.NormalizedUrl.Replace("{subdomain}", "subdomain", StringComparison.OrdinalIgnoreCase);
            if (!Uri.TryCreate(uriParseCandidate, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("仅支持 HTTP 或 HTTPS 的互联网切片服务链接。");
            }

            var normalizedTemplate = NormalizeTemplate(vendorNormalization.NormalizedUrl);
            var query = ParseQueryString(uri.Query);
            if (LooksLikeArcGisRestTile(uri.AbsolutePath))
            {
                return new InternetTileTemplateParseResult
                {
                    ServiceKind = InternetTileServiceKind.ArcGisRestTile,
                    TemplateMode = InternetTileTemplateMode.ArcGisRestTile,
                    NormalizedTemplate = normalizedTemplate,
                    SourceSpatialReferenceText = "EPSG:3857",
                    MatrixProfile = InternetTileMatrixProfile.WebMercator,
                    RowOrigin = vendorNormalization.RowOrigin,
                    VendorKind = vendorNormalization.VendorKind,
                    Subdomains = vendorNormalization.Subdomains
                };
            }

            if (LooksLikeRestfulWmts(vendorNormalization.NormalizedUrl, normalizedTemplate))
            {
                var normalizedUri = new Uri(uriParseCandidate);
                var pathSegments = Uri.UnescapeDataString(normalizedUri.AbsolutePath)
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var normalizedPathSegments = Uri.UnescapeDataString(new Uri(normalizedTemplate.Replace("{subdomain}", "subdomain", StringComparison.OrdinalIgnoreCase)).AbsolutePath)
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var versionIndex = Array.FindIndex(pathSegments, segment => VersionSegmentRegex.IsMatch(segment));
                var levelIndex = Array.FindIndex(normalizedPathSegments, segment => segment.Contains("{level}", StringComparison.Ordinal));
                var layerIdentifier = versionIndex >= 0 && versionIndex + 1 < pathSegments.Length ? pathSegments[versionIndex + 1] : null;
                var styleIdentifier = versionIndex >= 0 && versionIndex + 2 < pathSegments.Length ? pathSegments[versionIndex + 2] : null;
                var matrixSetIdentifier = levelIndex > 0 && levelIndex - 1 < pathSegments.Length ? pathSegments[levelIndex - 1] : null;
                return new InternetTileTemplateParseResult
                {
                    ServiceKind = InternetTileServiceKind.Wmts,
                    TemplateMode = InternetTileTemplateMode.RestfulWmts,
                    NormalizedTemplate = normalizedTemplate,
                    CapabilitiesUrl = BuildRestfulWmtsCapabilitiesUrl(uri),
                    LayerIdentifier = layerIdentifier,
                    MatrixSetIdentifier = matrixSetIdentifier,
                    StyleIdentifier = styleIdentifier,
                    SourceSpatialReferenceText = InferSpatialReferenceFromRestfulPath(pathSegments, matrixSetIdentifier),
                    MatrixProfile = InferMatrixProfile(matrixSetIdentifier),
                    RowOrigin = vendorNormalization.RowOrigin,
                    VendorKind = vendorNormalization.VendorKind,
                    Subdomains = vendorNormalization.Subdomains
                };
            }

            if (LooksLikeWmts(query))
            {
                var matrixSet = GetValue(query, "TILEMATRIXSET");
                var profile = InferMatrixProfile(matrixSet);
                return new InternetTileTemplateParseResult
                {
                    ServiceKind = InternetTileServiceKind.Wmts,
                    TemplateMode = InternetTileTemplateMode.QueryParameters,
                    NormalizedTemplate = normalizedTemplate,
                    CapabilitiesUrl = BuildWmtsCapabilitiesUrl(uri, query),
                    LayerIdentifier = GetValue(query, "LAYER"),
                    MatrixSetIdentifier = matrixSet,
                    StyleIdentifier = GetValue(query, "STYLE"),
                    Format = GetValue(query, "FORMAT"),
                    SourceSpatialReferenceText = profile switch
                    {
                        InternetTileMatrixProfile.WebMercator => "EPSG:3857",
                        InternetTileMatrixProfile.Geographic => "EPSG:4326",
                        _ => string.Empty
                    },
                    MatrixProfile = profile,
                    RowOrigin = vendorNormalization.RowOrigin,
                    VendorKind = vendorNormalization.VendorKind,
                    Subdomains = vendorNormalization.Subdomains
                };
            }

            if (LooksLikeQuadKey(normalizedTemplate))
            {
                return new InternetTileTemplateParseResult
                {
                    ServiceKind = InternetTileServiceKind.QuadKey,
                    TemplateMode = InternetTileTemplateMode.QuadKey,
                    NormalizedTemplate = normalizedTemplate,
                    SourceSpatialReferenceText = "EPSG:3857",
                    MatrixProfile = InternetTileMatrixProfile.WebMercator,
                    RowOrigin = vendorNormalization.RowOrigin,
                    VendorKind = vendorNormalization.VendorKind,
                    Subdomains = vendorNormalization.Subdomains
                };
            }

            if (LooksLikeXyz(normalizedTemplate))
            {
                return new InternetTileTemplateParseResult
                {
                    ServiceKind = InternetTileServiceKind.Xyz,
                    TemplateMode = uri.AbsolutePath.Contains("{level}", StringComparison.Ordinal) ? InternetTileTemplateMode.PathSegments : InternetTileTemplateMode.QueryParameters,
                    NormalizedTemplate = normalizedTemplate,
                    SourceSpatialReferenceText = "EPSG:3857",
                    MatrixProfile = InternetTileMatrixProfile.WebMercator,
                    RowOrigin = vendorNormalization.RowOrigin,
                    VendorKind = vendorNormalization.VendorKind,
                    Subdomains = vendorNormalization.Subdomains
                };
            }

            throw new InvalidOperationException("无法识别链接类型，当前仅支持 WMTS 或 XYZ 切片链接。");
        }

        private static string NormalizeTemplate(string url)
        {
            var normalized = url;
            foreach (var pair in PlaceholderAliases)
            {
                normalized = normalized.Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase);
            }

            return normalized;
        }

        private static bool LooksLikeWmts(IReadOnlyDictionary<string, string> query)
        {
            return string.Equals(GetValue(query, "SERVICE"), "WMTS", StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrWhiteSpace(GetValue(query, "TILEMATRIX")) &&
                    !string.IsNullOrWhiteSpace(GetValue(query, "TILEROW")) &&
                    !string.IsNullOrWhiteSpace(GetValue(query, "TILECOL")));
        }

        private static bool LooksLikeXyz(string normalizedTemplate)
        {
            return normalizedTemplate.Contains("{col}", StringComparison.Ordinal) &&
                   normalizedTemplate.Contains("{row}", StringComparison.Ordinal) &&
                   normalizedTemplate.Contains("{level}", StringComparison.Ordinal);
        }

        private static bool LooksLikeArcGisRestTile(string absolutePath)
        {
            return absolutePath.Contains("/MapServer/tile/", StringComparison.OrdinalIgnoreCase) ||
                   absolutePath.Contains("/ImageServer/tile/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeRestfulWmts(string rawUrl, string normalizedTemplate)
        {
            return (rawUrl.Contains("{TileMatrix}", StringComparison.OrdinalIgnoreCase) ||
                    rawUrl.Contains("{TileRow}", StringComparison.OrdinalIgnoreCase) ||
                    rawUrl.Contains("{TileCol}", StringComparison.OrdinalIgnoreCase)) &&
                   LooksLikeXyz(normalizedTemplate);
        }

        private static bool LooksLikeQuadKey(string normalizedTemplate)
        {
            return normalizedTemplate.Contains("{quadkey}", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildWmtsCapabilitiesUrl(Uri uri, IReadOnlyDictionary<string, string> query)
        {
            var orderedPairs = query
                .Where(pair => !IsTileRequestKey(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

            orderedPairs["SERVICE"] = "WMTS";
            orderedPairs["REQUEST"] = "GetCapabilities";
            if (!orderedPairs.ContainsKey("VERSION"))
            {
                orderedPairs["VERSION"] = "1.0.0";
            }

            var builder = new UriBuilder(uri)
            {
                Query = string.Join("&", orderedPairs.Select(pair => $"{pair.Key}={WebUtility.UrlEncode(pair.Value)}"))
            };
            return builder.Uri.ToString();
        }

        private static bool IsTileRequestKey(string key)
        {
            return key.Equals("TILEMATRIX", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("TILEROW", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("TILECOL", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("REQUEST", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetValue(IReadOnlyDictionary<string, string> query, string key)
        {
            return query.TryGetValue(key, out var value) ? value : null;
        }

        private static Dictionary<string, string> ParseQueryString(string queryString)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var trimmed = queryString.TrimStart('?');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return result;
            }

            var pairs = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pair in pairs)
            {
                var separatorIndex = pair.IndexOf('=');
                if (separatorIndex < 0)
                {
                    result[WebUtility.UrlDecode(pair)] = string.Empty;
                    continue;
                }

                var key = WebUtility.UrlDecode(pair[..separatorIndex]);
                var value = WebUtility.UrlDecode(pair[(separatorIndex + 1)..]);
                result[key] = value;
            }

            return result;
        }

        private static InternetTileMatrixProfile InferMatrixProfile(string? matrixSetIdentifier)
        {
            if (string.IsNullOrWhiteSpace(matrixSetIdentifier))
            {
                return InternetTileMatrixProfile.Unknown;
            }

            var normalized = matrixSetIdentifier.Trim().ToLowerInvariant();
            if (normalized == "w" || normalized.EndsWith("_w", StringComparison.Ordinal))
            {
                return InternetTileMatrixProfile.WebMercator;
            }

            if (normalized == "c" || normalized.EndsWith("_c", StringComparison.Ordinal))
            {
                return InternetTileMatrixProfile.Geographic;
            }

            return InternetTileMatrixProfile.Unknown;
        }

        private static string BuildRestfulWmtsCapabilitiesUrl(Uri uri)
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var versionIndex = Array.FindIndex(segments, segment => VersionSegmentRegex.IsMatch(segment));
            if (versionIndex < 0)
            {
                throw new InvalidOperationException("无法从 RESTful WMTS 链接推断能力文档地址。");
            }

            var capabilityPath = "/" + string.Join('/', segments.Take(versionIndex + 1)) + "/WMTSCapabilities.xml";
            return new UriBuilder(uri.Scheme, uri.Host, uri.Port, capabilityPath).Uri.ToString();
        }

        private static string InferSpatialReferenceFromRestfulPath(string[] pathSegments, string? matrixSetIdentifier)
        {
            if (!string.IsNullOrWhiteSpace(matrixSetIdentifier))
            {
                if (matrixSetIdentifier.Contains("4326", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(matrixSetIdentifier, "c", StringComparison.OrdinalIgnoreCase))
                {
                    return "EPSG:4326";
                }

                if (matrixSetIdentifier.Contains("3857", StringComparison.OrdinalIgnoreCase) ||
                    matrixSetIdentifier.Contains("900913", StringComparison.OrdinalIgnoreCase) ||
                    matrixSetIdentifier.Contains("102100", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(matrixSetIdentifier, "w", StringComparison.OrdinalIgnoreCase))
                {
                    return "EPSG:3857";
                }
            }

            var candidate = pathSegments.FirstOrDefault(segment =>
                segment.Contains("4326", StringComparison.OrdinalIgnoreCase) ||
                segment.Contains("3857", StringComparison.OrdinalIgnoreCase) ||
                segment.Contains("900913", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return "EPSG:3857";
            }

            return candidate.Contains("4326", StringComparison.OrdinalIgnoreCase)
                ? "EPSG:4326"
                : "EPSG:3857";
        }
    }
}
