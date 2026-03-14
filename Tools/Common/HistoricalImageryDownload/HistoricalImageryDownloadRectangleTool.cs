using System;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Tools.HistoricalImageryDownload
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

        protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            if (geometry?.Extent != null)
            {
                ExtentCreated?.Invoke(geometry.Extent);
            }

            await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
            return true;
        }
    }
}
