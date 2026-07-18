using System.Collections.Generic;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload
{
    /// <summary>
    /// 封装历史版本选择窗口的展示与取消还原行为，保持 ViewModel 不直接依赖 WPF 窗口。
    /// </summary>
    internal interface IHistoricalVersionSelectionDialogService
    {
        void Show(IList<HistoricalVersionSelectionItem> versions);
    }

    internal sealed class HistoricalVersionSelectionDialogService : IHistoricalVersionSelectionDialogService
    {
        public void Show(IList<HistoricalVersionSelectionItem> versions)
        {
            var dialog = new HistoricalVersionSelectionDialog(versions);
            _ = dialog.ShowDialog();
        }
    }
}
