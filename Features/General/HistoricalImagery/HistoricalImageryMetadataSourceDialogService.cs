using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.General.HistoricalImagery
{
    /// <summary>
    /// 在统一 UI 线程边界内展示 Wayback 元数据来源选择窗口。
    /// </summary>
    internal interface IHistoricalImageryMetadataSourceDialogService
    {
        Task<WaybackMetadataSourceCandidate?> SelectAsync(IReadOnlyList<WaybackMetadataSourceCandidate> candidates);
    }

    internal sealed class HistoricalImageryMetadataSourceDialogService : IHistoricalImageryMetadataSourceDialogService
    {
        public Task<WaybackMetadataSourceCandidate?> SelectAsync(IReadOnlyList<WaybackMetadataSourceCandidate> candidates) =>
            PresentationServices.UiThread.InvokeAsync(() =>
            {
                var dialog = new HistoricalImageryMetadataSourceDialog(candidates);
                var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
                if (owner is not null)
                {
                    dialog.Owner = owner;
                }

                return dialog.ShowDialog() == true ? dialog.SelectedCandidate : null;
            });
    }
}
