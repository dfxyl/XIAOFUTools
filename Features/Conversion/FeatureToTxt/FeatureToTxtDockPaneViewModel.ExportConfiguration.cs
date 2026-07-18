using XIAOFUTools.Features.Conversion.FeatureToTxt.Core;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    internal partial class FeatureToTxtDockPaneViewModel
    {
        private FeatureToTxtExportConfiguration CreateExportConfiguration()
        {
            return new FeatureToTxtExportConfiguration(
                Prefix,
                DecimalPlaces,
                OutputClosingPoint,
                InnerRingStartFromOne,
                ClosingPointContinueNumbering,
                SwapXY);
        }
    }
}
