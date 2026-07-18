using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Win32;

namespace XIAOFUTools.Features.General.QuickAddData
{
    internal sealed partial class QuickAddDataViewModel
    {

        public ObservableCollection<QuickDataTreeItemViewModel> TreeNodes
        {
            get => _treeNodes;
            set => SetProperty(ref _treeNodes, value);
        }


        public QuickDataTreeItemViewModel SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }


        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }


        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }


        public ICommand CreateGroupCommand { get; }


        public ICommand RenameSelectedCommand { get; }


        public ICommand AddFilesCommand { get; }


        public ICommand AddFilesToSelectedGroupCommand { get; }


        public ICommand ImportFolderCommand { get; }


        public ICommand ImportToSelectedGroupCommand { get; }


        public ICommand DeleteSelectedCommand { get; }


        public ICommand RefreshCommand { get; }


        public ICommand RefreshSelectedDatabaseCommand { get; }


        public ICommand LoadSelectedNodeCommand { get; }


        public ICommand ShowHelpCommand { get; }


        public QuickAddDataTreeDragDropHandler TreeDragDropHandler => _treeDragDropHandler;


        public IReadOnlyList<QuickDataTreeItemViewModel> SelectedNodes => EnumerateTreeNodes(TreeNodes)
            .Where(node => node.IsSelected)
            .ToList();

    }
}
