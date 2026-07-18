#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Services
{
    internal static class HistoricalBatchDownloadPlanner
    {
        public static IReadOnlyList<HistoricalDownloadRequest> CreateRequests(
            HistoricalImageryProviderType provider,
            HistoricalAreaSourceType areaSourceType,
            IEnumerable<HistoricalVersionSelectionItem> selections,
            int zoomLevel,
            string outputFolderPath,
            GoogleNearestDateFallbackMode googleNearestDateFallbackMode)
        {
            return selections
                .Where(selection => selection.IsSelected)
                .Select(selection => new HistoricalDownloadRequest
                {
                    Provider = provider,
                    AreaSourceType = areaSourceType,
                    Version = selection.Version,
                    ZoomLevel = zoomLevel,
                    OutputFilePath = HistoricalOutputPathBuilder.BuildForFolder(outputFolderPath, selection.Version, zoomLevel),
                    UseCache = true,
                    GoogleNearestDateFallbackMode = googleNearestDateFallbackMode
                })
                .ToArray();
        }
    }
}
