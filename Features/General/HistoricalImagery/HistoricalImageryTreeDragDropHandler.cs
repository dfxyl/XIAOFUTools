using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.DragDrop;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ESRI.ArcGIS.ItemIndex;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;

namespace XIAOFUTools.Features.General.HistoricalImagery
{
    internal sealed class HistoricalImageryTreeDragDropHandler : IDragSource
    {
        private static readonly FieldInfo ItemInfoValueField = typeof(Item).GetField("_itemInfoValue", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly HistoricalImageryDockPaneViewModel _viewModel;

        public HistoricalImageryTreeDragDropHandler(HistoricalImageryDockPaneViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void StartDrag(DragInfo dragInfo)
        {
            var node = dragInfo.SourceItem as TreeNode ?? _viewModel.SelectedNode;
            if (node?.Version == null || !_viewModel.TryCreateLayerRequest(node.Version, out var request, out _))
            {
                dragInfo.Effects = DragDropEffects.None;
                return;
            }

            _viewModel.RegisterPendingDragRequest(request);

            var clipboardItem = TryCreateClipboardItem(request);
            if (clipboardItem == null)
            {
                _viewModel.ClearPendingDragRequest();
                dragInfo.Effects = DragDropEffects.None;
                return;
            }

            dragInfo.Data = new List<ClipboardItem> { clipboardItem };
            dragInfo.Effects = DragDropEffects.Copy;
        }

        private static ClipboardItem TryCreateClipboardItem(HistoricalImageryLayerRequest request)
        {
            return QueuedTask.Run(() =>
            {
                var item = ItemFactory.Instance.Create(request.LayerUri.AbsoluteUri, ItemFactory.ItemType.PortalItem);
                if (item == null)
                {
                    return null;
                }

                if (ItemInfoValueField?.GetValue(item) is not ItemInfoValue itemInfoValue)
                {
                    return null;
                }

                return new ClipboardItem
                {
                    ItemInfoValue = itemInfoValue,
                    Data = request.LayerUri.AbsoluteUri
                };
            }).Result;
        }
    }
}
