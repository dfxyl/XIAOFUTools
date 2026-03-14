#nullable enable

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    internal static class HistoricalVersionSelectionSummaryBuilder
    {
        public static string Build(int totalCount, int selectedCount)
        {
            if (totalCount <= 0)
            {
                return "未查询下载时间";
            }

            if (selectedCount <= 0)
            {
                return $"共 {totalCount} 个时间，未勾选";
            }

            return $"已勾选 {selectedCount} 个时间，共 {totalCount} 个";
        }
    }
}
