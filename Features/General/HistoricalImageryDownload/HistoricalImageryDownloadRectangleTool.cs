using System;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload
{
    internal class HistoricalImageryDownloadRectangleTool : MapTool
    {
        public static event Action<Envelope>? ExtentCreated;

        public HistoricalImageryDownloadRectangleTool()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Rectangle;
            SketchOutputMode = SketchOutputMode.Map;
            SketchSymbol = null;
        }

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            if (geometry?.Extent != null)
            {
                ExtentCreated?.Invoke(geometry.Extent);
            }

            return Task.FromResult(true);
        }
    }
}
