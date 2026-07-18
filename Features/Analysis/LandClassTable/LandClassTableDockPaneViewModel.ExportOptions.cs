using System;
using System.Globalization;
using XIAOFUTools.Features.Analysis.LandClassTable.Core;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    internal sealed partial class LandClassTableDockPaneViewModel
    {
        private void ApplyReportProfile(LandClassReportProfile profile)
        {
            ReportYear = profile.ReportYear;
            LocationName = profile.LocationName;
            ReportCompany = profile.ReportCompany;
            PreparedBy = profile.PreparedBy;
            ReviewedBy = profile.ReviewedBy;
        }

        private LandClassExportOptions CreateExportOptions(string rightHolderName)
        {
            return new LandClassExportOptions(
                OutputFolder,
                UseGroupedOutput && SelectedGroupField != null && !SelectedGroupField.IsEmptyOption,
                UsePlotNameColumn,
                DecimalPlaces,
                ReportYear,
                LocationName,
                SelectedAreaUnit,
                ReportCompany,
                PreparedBy,
                ReviewedBy,
                string.IsNullOrWhiteSpace(ReportDate)
                    ? DateTime.Now.ToString("yyyy年M月d日", CultureInfo.CurrentCulture)
                    : ReportDate,
                rightHolderName);
        }
    }
}
