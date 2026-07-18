namespace XIAOFUTools.Features.Custom.GisToolPocket.Core
{
    internal enum ArcGisCommandKind
    {
        Unsupported,
        Command,
        MapTool,
        GeoprocessingTool
    }

    internal sealed class ArcGisCommandDefinition
    {
        public string Id { get; set; } = string.Empty;

        public string Caption { get; set; } = string.Empty;

        public string ClassName { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public ArcGisCommandKind Kind { get; set; }
    }
}
