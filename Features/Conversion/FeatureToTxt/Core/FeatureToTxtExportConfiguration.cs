namespace XIAOFUTools.Features.Conversion.FeatureToTxt.Core
{
    internal sealed record FeatureToTxtExportConfiguration(
        string PointPrefix,
        int DecimalPlaces,
        bool OutputClosingPoint,
        bool InnerRingStartsAtOne,
        bool ClosingPointContinuesNumbering,
        bool SwapCoordinates);
}
