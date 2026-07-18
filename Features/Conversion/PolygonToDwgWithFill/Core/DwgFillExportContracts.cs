namespace XIAOFUTools.Features.Conversion.PolygonToDwgWithFill.Core
{
    public enum DwgExportVersion
    {
        AutoCad2018,
        AutoCad2013,
        AutoCad2010,
        AutoCad2007,
        AutoCad2004,
        AutoCad2000
    }

    internal sealed record DwgFillExportOptions(
        DwgExportVersion Version,
        bool ExportBoundary,
        bool ExportHatch,
        double LineWidthMillimeters,
        int HatchTransparency);

    internal sealed record DwgFillExportProgress(int ProcessedCount, int TotalCount);

    internal sealed record DwgFillExportResult(string OutputPath, int ProcessedCount, bool Cancelled);
}
