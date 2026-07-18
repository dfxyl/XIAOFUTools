using System.Collections.Generic;
using System.Data;
using ArcGIS.Core.Geometry;

namespace XIAOFUTools.Features.Analysis.MultiOverlaySummary
{
    /// <summary>
    /// 多图层压盖汇总的结果展示和导出选项展示边界。
    /// </summary>
    internal interface IMultiOverlaySummaryDialogService
    {
        void ShowResult(
            DataTable resultTable,
            int decimalPlaces,
            List<IntersectGeometryItem> intersectGeometries,
            SpatialReference spatialReference,
            string areaUnit);

        MultiOverlayExportOptions? SelectExportOptions();
    }

    internal sealed record MultiOverlayExportOptions(bool ExportExcel, bool ExportGdb);

    internal sealed class MultiOverlaySummaryDialogService : IMultiOverlaySummaryDialogService
    {
        public void ShowResult(
            DataTable resultTable,
            int decimalPlaces,
            List<IntersectGeometryItem> intersectGeometries,
            SpatialReference spatialReference,
            string areaUnit)
        {
            var window = new MultiOverlaySummaryResultWindow(
                resultTable,
                decimalPlaces,
                intersectGeometries,
                spatialReference,
                areaUnit);
            _ = window.ShowDialog();
        }

        public MultiOverlayExportOptions? SelectExportOptions()
        {
            var dialog = new ExportOptionsDialog();
            return dialog.ShowDialog() == true
                ? new MultiOverlayExportOptions(dialog.ExportExcel, dialog.ExportGdb)
                : null;
        }
    }
}
