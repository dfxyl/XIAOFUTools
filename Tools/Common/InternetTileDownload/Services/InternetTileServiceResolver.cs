#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Tools.InternetTileDownload.Infrastructure;

namespace XIAOFUTools.Tools.InternetTileDownload.Services
{
    internal interface IInternetTileHttpClient
    {
        Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default);

        Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default);
    }

    internal sealed class InternetTileServiceResolver
    {
        private const int DefaultMaxLevel = 22;
        private const double WebMercatorOriginShift = 20037508.342789244d;
        private readonly IInternetTileHttpClient _httpClient;

        public InternetTileServiceResolver(IInternetTileHttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new InternetTileHttpClient();
        }

        public async Task<InternetTileServiceDefinition> ResolveAsync(string url, CancellationToken cancellationToken = default)
        {
            var templateInfo = InternetTileTemplateParser.Parse(url);
            if (templateInfo.ServiceKind != InternetTileServiceKind.Wmts)
            {
                return CreateStandardTileDefinition(templateInfo);
            }

            var capabilitiesUrl = templateInfo.CapabilitiesUrl
                ?? throw new InvalidOperationException("WMTS 链接未生成能力文档地址。");
            var xml = await _httpClient.GetStringAsync(capabilitiesUrl, cancellationToken);
            return InternetWmtsCapabilitiesParser.Parse(xml, templateInfo);
        }

        private static InternetTileServiceDefinition CreateStandardTileDefinition(InternetTileTemplateParseResult templateInfo)
        {
            var tileSize = 256;
            var initialResolution = (WebMercatorOriginShift * 2d) / tileSize;
            var levels = Enumerable.Range(0, DefaultMaxLevel + 1)
                .Select(level => new InternetTileLevelDefinition
                {
                    LevelId = level.ToString(),
                    Resolution = initialResolution / (1 << level),
                    TopLeftX = -WebMercatorOriginShift,
                    TopLeftY = WebMercatorOriginShift,
                    TileWidth = tileSize,
                    TileHeight = tileSize,
                    MatrixWidth = 1 << level,
                    MatrixHeight = 1 << level
                })
                .ToArray();

            return new InternetTileServiceDefinition
            {
                ServiceKind = templateInfo.ServiceKind,
                UrlTemplate = templateInfo.NormalizedTemplate,
                SourceSpatialReferenceText = templateInfo.SourceSpatialReferenceText,
                MatrixProfile = templateInfo.MatrixProfile,
                TemplateMode = templateInfo.TemplateMode,
                RowOrigin = templateInfo.RowOrigin,
                VendorKind = templateInfo.VendorKind,
                Subdomains = templateInfo.Subdomains,
                Levels = levels
            };
        }
    }
}
