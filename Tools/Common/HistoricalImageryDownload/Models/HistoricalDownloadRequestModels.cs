#nullable enable

namespace XIAOFUTools.Tools.HistoricalImageryDownload
{
    public enum HistoricalAreaSourceType
    {
        CurrentView = 0,
        CustomExtent = 1,
        FeatureLayer = 2
    }

    public enum GoogleNearestDateFallbackMode
    {
        SeparateOutputs = 0,
        MixedSingleOutput = 1
    }

    public sealed class HistoricalDownloadRequest
    {
        public HistoricalImageryProviderType Provider { get; set; }

        public HistoricalAreaSourceType AreaSourceType { get; set; }

        public HistoricalVersionItem Version { get; set; } = new();

        public int ZoomLevel { get; set; }

        public string OutputFilePath { get; set; } = string.Empty;

        public bool UseCache { get; set; } = true;

        public GoogleNearestDateFallbackMode GoogleNearestDateFallbackMode { get; set; } = GoogleNearestDateFallbackMode.SeparateOutputs;

        public bool UseCurrentMapSpatialReferenceByDefault { get; set; } = true;
    }
}
