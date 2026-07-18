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
    internal sealed partial class QuickAddDataViewModel : PropertyChangedBase
    {
        private const string DefaultGroupName = "默认分组";

        private readonly QuickDataLibraryStore _libraryStore;
        private readonly QuickDataImportService _importService;
        private readonly QuickDataMapLoadService _mapLoadService;
        private readonly QuickAddDataTreeDragDropHandler _treeDragDropHandler;

        private QuickDataLibraryDocument _document;
        private ObservableCollection<QuickDataTreeItemViewModel> _treeNodes;
        private QuickDataTreeItemViewModel _selectedNode;
        private string _selectionAnchorKey;
        private string _statusMessage = "快捷数据面板已就绪。";
        private bool _isBusy;
        private bool _isRestoringTreeState;

        public QuickAddDataViewModel()
        {
            _libraryStore = new QuickDataLibraryStore();
            _importService = new QuickDataImportService(new ArcGisQuickDataGeodatabaseInspector());
            _mapLoadService = new QuickDataMapLoadService();
            _treeDragDropHandler = new QuickAddDataTreeDragDropHandler(this);
            _treeNodes = new ObservableCollection<QuickDataTreeItemViewModel>();
            _document = new QuickDataLibraryDocument();

            CreateGroupCommand = new RelayCommand(CreateGroup);
            RenameSelectedCommand = new RelayCommand(RenameSelected);
            AddFilesCommand = new RelayCommand(AddFilesToTargetGroup);
            AddFilesToSelectedGroupCommand = new RelayCommand(AddFilesToSelectedGroup);
            ImportFolderCommand = new RelayCommand(ImportFolderToTargetGroup);
            ImportToSelectedGroupCommand = new RelayCommand(ImportFolderToSelectedGroup);
            DeleteSelectedCommand = new RelayCommand(DeleteSelected);
            RefreshCommand = new RelayCommand(RefreshLibraryOrSelectedNode);
            RefreshSelectedDatabaseCommand = new RelayCommand(async () => await RefreshSelectedDatabaseAsync());
            LoadSelectedNodeCommand = new RelayCommand(async () => await LoadSelectedNodeAsync());
            ShowHelpCommand = new RelayCommand(ShowHelp);

            ReloadLibrary();
        }
    }
}
