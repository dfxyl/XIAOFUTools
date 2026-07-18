using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.DragDrop;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ESRI.ArcGIS.ItemIndex;

namespace XIAOFUTools.Features.General.QuickAddData
{
    internal sealed class QuickAddDataTreeDragDropHandler : IDragSource, IDropTarget
    {
        private readonly QuickAddDataViewModel _viewModel;
        private static readonly FieldInfo ItemInfoValueField = typeof(Item).GetField("_itemInfoValue", BindingFlags.Instance | BindingFlags.NonPublic);

        public QuickAddDataTreeDragDropHandler(QuickAddDataViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public IReadOnlyList<QuickDataTreeItemViewModel> CurrentDragNodes { get; private set; } = new List<QuickDataTreeItemViewModel>();

        public void StartDrag(DragInfo dragInfo)
        {
            var draggedNode = dragInfo.SourceItem as QuickDataTreeItemViewModel ?? _viewModel.SelectedNode;
            var dragNodes = _viewModel.GetDragSelection(draggedNode);
            if (dragNodes.Count == 0)
            {
                dragInfo.Effects = DragDropEffects.None;
                return;
            }

            CurrentDragNodes = dragNodes;

            var dragPaths = dragNodes
                .SelectMany(node => _viewModel.GetDragCatalogPaths(node))
                .Distinct()
                .ToList();

            if (dragPaths.Count == 0)
            {
                dragInfo.Effects = DragDropEffects.Move;
                return;
            }

            var clipboardItems = QueuedTask.Run(() =>
            {
                var result = new List<ClipboardItem>();
                foreach (var path in dragPaths)
                {
                    var item = ItemFactory.Instance.Create(path, ItemFactory.ItemType.PathItem);
                    if (item != null)
                    {
                        var itemInfoValue = ItemInfoValueField?.GetValue(item);
                        if (itemInfoValue == null)
                        {
                            continue;
                        }

                        result.Add(new ClipboardItem
                        {
                            ItemInfoValue = (ItemInfoValue)itemInfoValue,
                            Data = path
                        });
                    }
                }

                return result;
            }).Result;

            if (clipboardItems.Count == 0)
            {
                dragInfo.Effects = DragDropEffects.Move;
                return;
            }

            dragInfo.Data = clipboardItems;
            dragInfo.Effects = DragDropEffects.Move | DragDropEffects.Copy;
        }

        public void OnDragOver(DropInfo dropInfo)
        {
            var targetNode = dropInfo.TargetItem as QuickDataTreeItemViewModel;
            if (CurrentDragNodes.Count == 0 || targetNode == null)
            {
                dropInfo.Effects = DragDropEffects.None;
                return;
            }

            dropInfo.Effects = CurrentDragNodes.All(node => _viewModel.CanDropTreeNode(node, targetNode))
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }

        public void OnDrop(DropInfo dropInfo)
        {
            var targetNode = dropInfo.TargetItem as QuickDataTreeItemViewModel;
            if (CurrentDragNodes.Count == 0 || targetNode == null)
            {
                return;
            }

            _viewModel.MoveTreeNodes(CurrentDragNodes, targetNode);
            CurrentDragNodes = new List<QuickDataTreeItemViewModel>();
        }
    }
}
