using System.Data;

namespace XIAOFUTools.Features.Analysis.IntersectSummary
{
    /// <summary>
    /// 封装交集汇总结果窗口展示。
    /// </summary>
    internal interface IIntersectSummaryResultWindowService
    {
        void Show(DataTable resultTable, int decimalPlaces, int regionFieldCount);
    }

    internal sealed class IntersectSummaryResultWindowService : IIntersectSummaryResultWindowService
    {
        public void Show(DataTable resultTable, int decimalPlaces, int regionFieldCount)
        {
            var window = new IntersectSummaryResultWindow(resultTable, decimalPlaces, regionFieldCount);
            _ = window.ShowDialog();
        }
    }
}
