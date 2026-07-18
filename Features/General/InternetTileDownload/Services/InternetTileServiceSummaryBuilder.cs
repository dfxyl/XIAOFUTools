#nullable enable

namespace XIAOFUTools.Features.General.InternetTileDownload.Services
{
    internal static class InternetTileServiceSummaryBuilder
    {
        public static string Build(InternetTileServiceDefinition definition)
        {
            var layerText = string.IsNullOrWhiteSpace(definition.LayerIdentifier) ? string.Empty : $" 图层 {definition.LayerIdentifier}";
            var matrixText = string.IsNullOrWhiteSpace(definition.MatrixSetIdentifier) ? string.Empty : $" 矩阵集 {definition.MatrixSetIdentifier}";
            return $"{definition.ServiceKind} | {definition.TemplateMode} | {definition.VendorKind} | {definition.RowOrigin} | {definition.SourceSpatialReferenceText} | {definition.Levels.Count} 个级别{layerText}{matrixText}";
        }
    }
}
