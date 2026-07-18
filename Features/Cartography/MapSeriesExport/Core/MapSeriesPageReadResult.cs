using System.Collections.Generic;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport.Core
{
    internal enum MapSeriesPageReadStatus
    {
        Loaded,
        LayoutUnavailable,
        MapSeriesDisabled,
        Failed
    }

    internal sealed class MapSeriesPageDescriptor
    {
        public int PageIndex { get; init; }

        public string PageName { get; init; }
    }

    internal sealed class MapSeriesPageReadResult
    {
        public MapSeriesPageReadStatus Status { get; init; }

        public string Message { get; init; }

        public IReadOnlyList<MapSeriesPageDescriptor> Pages { get; init; } =
            new List<MapSeriesPageDescriptor>();
    }
}
