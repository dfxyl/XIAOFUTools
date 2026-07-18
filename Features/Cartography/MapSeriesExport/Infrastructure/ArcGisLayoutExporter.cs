using System;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport.Infrastructure
{
    internal sealed class ArcGisLayoutExporter
    {
        public void Export(Layout layout, string format, string filePath, int resolution)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            switch (format?.ToUpperInvariant())
            {
                case "PDF":
                    layout.Export(new PDFFormat
                    {
                        Resolution = resolution,
                        OutputFileName = filePath,
                        ImageQuality = ImageQuality.Best
                    });
                    break;
                case "JPG":
                    layout.Export(new JPEGFormat
                    {
                        Resolution = resolution,
                        OutputFileName = filePath
                    });
                    break;
                case "PNG":
                    layout.Export(new PNGFormat
                    {
                        Resolution = resolution,
                        OutputFileName = filePath
                    });
                    break;
                case "TIF":
                    layout.Export(new TIFFFormat
                    {
                        Resolution = resolution,
                        OutputFileName = filePath
                    });
                    break;
                default:
                    throw new NotSupportedException($"不支持的布局导出格式: {format}");
            }
        }
    }
}
