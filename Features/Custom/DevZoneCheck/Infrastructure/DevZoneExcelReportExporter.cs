using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Features.Custom.DevZoneCheck.Infrastructure
{
    internal sealed record DevZoneReportOptions(
        string AreaUnit, int DecimalPlaces, bool UrbanBoundary, bool PermanentFarmland,
        bool EcoRedline, bool LandSurvey, bool ApprovedLand, bool ApprovedNotSupplied,
        bool SuppliedLand, bool IdleLand, bool SpatialPlanning, bool EvidenceData,
        bool SupplyYearData);

    internal static class DevZoneExcelReportExporter
    {
        public static Task ExportAsync(
            IReadOnlyList<CheckResultData> results,
            DevZoneReportOptions options,
            string path,
            CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { Export(results, options, path, cancellationToken); completion.SetResult(); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { completion.SetCanceled(cancellationToken); }
                catch (Exception ex) { completion.SetException(ex); }
            }) { IsBackground = true, Name = "XIAOFUTools-DevZoneExcelExport" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static void Export(IReadOnlyList<CheckResultData> results, DevZoneReportOptions options, string path, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(results); ArgumentNullException.ThrowIfNull(options);
            Excel.Application? app = null; Excel.Workbook? book = null; Excel.Worksheet? sheet = null;
            try
            {
                app = new Excel.Application { Visible = false, DisplayAlerts = false };
                book = app.Workbooks.Add(); sheet = (Excel.Worksheet)book.Sheets[1]; sheet.Name = "核查报告";
                var headers = Headers(options);
                for (var i = 0; i < headers.Count; i++) sheet.Cells[1, i + 1] = headers[i];
                var row = 2;
                foreach (var result in results)
                {
                    token.ThrowIfCancellationRequested(); var values = Values(result, options);
                    for (var col = 0; col < values.Count; col++) sheet.Cells[row, col + 1] = values[col];
                    row++;
                }
                Excel.Range? header = null;
                try { header = sheet.Range[sheet.Cells[1, 1], sheet.Cells[1, headers.Count]]; header.Font.Bold = true; header.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray); }
                finally { Release(header); }
                sheet.Columns.AutoFit(); book.SaveAs(path);
            }
            finally { Release(sheet); if (book != null) { try { book.Close(false); } catch (COMException) { } Release(book); } if (app != null) { try { app.Quit(); } catch (COMException) { } Release(app); } }
        }

        private static List<string> Headers(DevZoneReportOptions o)
        {
            var h = new List<string> { "园区名称", $"园区面积({o.AreaUnit})" };
            if (o.UrbanBoundary) h.AddRange([$"开发边界内({o.AreaUnit})", $"开发边界外({o.AreaUnit})"]);
            if (o.PermanentFarmland) h.Add($"压占永久基本农田({o.AreaUnit})"); if (o.EcoRedline) h.Add($"压占生态保护红线({o.AreaUnit})");
            if (o.LandSurvey) h.AddRange([$"现状建设用地({o.AreaUnit})", $"现状工业用地({o.AreaUnit})", $"现状采矿用地({o.AreaUnit})", $"现状盐田({o.AreaUnit})", $"现状仓储用地({o.AreaUnit})", "现状工业用地率(%)"]);
            if (o.ApprovedLand) h.Add($"已批建设用地({o.AreaUnit})"); if (o.ApprovedNotSupplied) h.AddRange([$"批而未供面积({o.AreaUnit})", "批而未供率(%)"]); if (o.SuppliedLand) h.Add($"已供应建设用地({o.AreaUnit})"); if (o.IdleLand) h.AddRange([$"闲置土地面积({o.AreaUnit})", "闲置土地率(%)"]); if (o.SpatialPlanning) h.AddRange([$"规划建设用地({o.AreaUnit})", $"规划工矿仓储用地({o.AreaUnit})", "规划工业用地率(%)"]); if (o.EvidenceData) h.Add($"举证面积({o.AreaUnit})"); if (o.SupplyYearData) h.Add("尚可供应年限(年)"); return h;
        }

        private static List<object> Values(CheckResultData r, DevZoneReportOptions o)
        {
            var v = new List<object> { r.ParkName, Round(r.ParkArea, o.DecimalPlaces) };
            if (o.UrbanBoundary) v.AddRange([Round(r.AreaInUrbanBoundary,o.DecimalPlaces),Round(r.AreaOutUrbanBoundary,o.DecimalPlaces)]); if (o.PermanentFarmland) v.Add(Round(r.AreaOnPermanentFarmland,o.DecimalPlaces)); if (o.EcoRedline) v.Add(Round(r.AreaOnEcoRedline,o.DecimalPlaces)); if (o.LandSurvey) v.AddRange([Round(r.CurrentConstructionLandArea,o.DecimalPlaces),Round(r.CurrentIndustrialLandArea,o.DecimalPlaces),Round(r.CurrentMiningLandArea,o.DecimalPlaces),Round(r.CurrentSaltFieldArea,o.DecimalPlaces),Round(r.CurrentWarehouseLandArea,o.DecimalPlaces),Round(r.CurrentIndustrialRate,2)]); if (o.ApprovedLand) v.Add(Round(r.ApprovedLandArea,o.DecimalPlaces)); if (o.ApprovedNotSupplied) v.AddRange([Round(r.ApprovedNotSuppliedArea,o.DecimalPlaces),Round(r.ApprovedNotSuppliedRate,2)]); if (o.SuppliedLand) v.Add(Round(r.SuppliedLandArea,o.DecimalPlaces)); if (o.IdleLand) v.AddRange([Round(r.IdleLandArea,o.DecimalPlaces),Round(r.IdleLandRate,2)]); if (o.SpatialPlanning) v.AddRange([Round(r.PlannedConstructionLandArea,o.DecimalPlaces),Round(r.PlannedIndustrialLandArea,o.DecimalPlaces),Round(r.PlannedIndustrialRate,2)]); if (o.EvidenceData) v.Add(Round(r.EvidenceArea,o.DecimalPlaces)); if (o.SupplyYearData) v.Add(Round(r.AvailableSupplyYears,1)); return v;
        }

        private static double Round(double value, int digits) => Math.Round(value, digits);
        private static void Release(object? value) { if (value == null) return; try { Marshal.ReleaseComObject(value); } catch (COMException) { } catch (InvalidComObjectException) { } }
    }
}
