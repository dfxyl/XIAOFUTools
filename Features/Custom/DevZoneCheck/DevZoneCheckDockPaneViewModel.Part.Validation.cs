using System;
using System.Windows;

namespace XIAOFUTools.Features.Custom.DevZoneCheck
{
    internal partial class DevZoneCheckDockPaneViewModel
    {
        private bool ValidateCheckInput()
        {
            if (SelectedParkLayer == null)
            {
                ShowValidationWarning("请选择园区红线图层！");
                return false;
            }

            if (EnableLandSurvey && LandSurveyLayer != null && string.IsNullOrEmpty(SelectedLandSurveyField))
            {
                ShowValidationWarning("已勾选变更调查数据，请选择地类代码字段！");
                return false;
            }

            if (EnableSpatialPlanning && SpatialPlanningLayer != null &&
                string.IsNullOrEmpty(SelectedSpatialPlanningField))
            {
                ShowValidationWarning("已勾选国土空间规划，请选择用地代码字段！");
                return false;
            }

            if (EnableApprovedNotSupplied && ApprovedNotSuppliedLayer != null && ApprovedLandLayer == null)
            {
                ShowValidationWarning("批而未供率计算需要已批建设用地数据！");
                return false;
            }

            if (EnableIdleLand && IdleLandLayer != null && SuppliedLandLayer == null)
            {
                ShowValidationWarning("闲置土地率计算需要已供应建设用地数据！");
                return false;
            }

            if (EnableEvidenceData && EvidenceDataLayer != null && SpatialPlanningLayer == null)
            {
                ShowValidationWarning("尚可供应年限计算需要国土空间规划数据！");
                return false;
            }

            if (EnableEvidenceData && EvidenceDataLayer != null && SuppliedLandLayer == null)
            {
                ShowValidationWarning("尚可供应年限计算需要已供应建设用地数据！");
                return false;
            }

            return true;
        }

        private static void ShowValidationWarning(string message)
        {
            PresentationServices.Dialogs.Show(
                message,
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
