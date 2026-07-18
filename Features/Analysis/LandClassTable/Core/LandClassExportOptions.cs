namespace XIAOFUTools.Features.Analysis.LandClassTable.Core
{
    internal sealed record LandClassExportOptions(
        string OutputFolder,
        bool UseGroupedOutput,
        bool IncludePlotName,
        int DecimalPlaces,
        string ReportYear,
        string LocationName,
        string AreaUnit,
        string ReportCompany,
        string PreparedBy,
        string ReviewedBy,
        string ReportDate,
        string RightHolderName);
}
