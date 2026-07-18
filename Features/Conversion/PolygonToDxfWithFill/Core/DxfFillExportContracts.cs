using System;

namespace XIAOFUTools.Features.Conversion.PolygonToDxfWithFill.Core
{
    public enum DxfExportVersion
    {
        AutoCad2018,
        AutoCad2013,
        AutoCad2010,
        AutoCad2007,
        AutoCad2004,
        AutoCad2000
    }

    internal sealed record DxfFillExportOptions(
        DxfExportVersion Version,
        bool ExportBoundary,
        bool ExportHatch,
        double LineWidthMillimeters,
        int HatchTransparency);

    internal sealed record DxfFillExportProgress(int ProcessedCount, int TotalCount);

    internal sealed record DxfFillExportResult(
        string OutputPath,
        int ProcessedCount,
        bool Cancelled);
}
