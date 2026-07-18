using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.General.QuickAddData;

namespace XIAOFUTools.Features.DataManagement.BatchAddData
{
    public class DataItem : PropertyChangedBase
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public int Index { get; set; }

        public string Name { get; set; }

        public string DataType { get; set; }

        public string CoordinateSystem { get; set; }

        public string FullPath { get; set; }

        public QuickDataNode SourceNode { get; set; }
    }

    internal class BatchAddDataViewModel : PropertyChangedBase
    {
        private readonly Infrastructure.BatchAddDataSearchPathValidator _searchPathValidator = new Infrastructure.BatchAddDataSearchPathValidator();
        private readonly QuickDataImportService _importService;
        private readonly QuickDataMapLoadService _mapLoadService;

        private string _searchPath = string.Empty;
        private bool _isFeatureClass = true;
        private bool _isTable = true;
        private bool _isRaster = true;
        private bool _searchSubfolders = true;
        private string _keyword = string.Empty;
        private ObservableCollection<DataItem> _dataItems;
        private string _groupName = string.Empty;
        private bool _isProcessing;
        private string _statusMessage = "请选择搜索路径并点击搜索按钮。";
        private int _progress;
        private bool _isProgressIndeterminate;

        private ICommand _browseFolderCommand;
        private ICommand _searchCommand;
        private ICommand _selectAllCommand;
        private ICommand _invertSelectionCommand;
        private ICommand _addDataCommand;
        private ICommand _showHelpCommand;

        public BatchAddDataViewModel()
        {
            _importService = new QuickDataImportService(new ArcGisQuickDataGeodatabaseInspector());
            _mapLoadService = new QuickDataMapLoadService();
            DataItems = new ObservableCollection<DataItem>();

            try
            {
                SearchPath = Project.Current != null
                    ? Path.GetDirectoryName(Project.Current.Path)
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            catch
            {
                SearchPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        public string SearchPath
        {
            get => _searchPath;
            set
            {
                SetProperty(ref _searchPath, value);
                NotifyCanExecuteChanged();
            }
        }

        public bool IsFeatureClass
        {
            get => _isFeatureClass;
            set => SetProperty(ref _isFeatureClass, value);
        }

        public bool IsTable
        {
            get => _isTable;
            set => SetProperty(ref _isTable, value);
        }

        public bool IsRaster
        {
            get => _isRaster;
            set => SetProperty(ref _isRaster, value);
        }

        public bool SearchSubfolders
        {
            get => _searchSubfolders;
            set => SetProperty(ref _searchSubfolders, value);
        }

        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        public ObservableCollection<DataItem> DataItems
        {
            get => _dataItems;
            set => SetProperty(ref _dataItems, value);
        }

        public string GroupName
        {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
                NotifyCanExecuteChanged();
            }
        }

        public bool CanProcess => !IsProcessing;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        public ICommand BrowseFolderCommand => _browseFolderCommand ??= new RelayCommand(BrowseFolder);

        public ICommand SearchCommand => _searchCommand ??= new RelayCommand(SearchData, CanSearch);

        public ICommand SelectAllCommand => _selectAllCommand ??= new RelayCommand(SelectAll);

        public ICommand InvertSelectionCommand => _invertSelectionCommand ??= new RelayCommand(InvertSelection);

        public ICommand AddDataCommand => _addDataCommand ??= new RelayCommand(AddData, CanAddData);

        public ICommand ShowHelpCommand => _showHelpCommand ??= new RelayCommand(ShowHelp);

        private void BrowseFolder()
        {
            try
            {
                var selectedPath = PresentationServices.Files.SelectFolder(
                    "选择要扫描的文件夹",
                    string.IsNullOrWhiteSpace(SearchPath) ? null : GetNormalizedSearchPath());
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    SearchPath = selectedPath;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"打开文件夹选择器失败: {ex.Message}";
            }
        }

        private bool CanSearch()
        {
            return !string.IsNullOrWhiteSpace(SearchPath) && !IsProcessing;
        }

        private async void SearchData()
        {
            if (!CanSearch())
            {
                StatusMessage = "请先选择有效的搜索路径。";
                return;
            }

            IsProcessing = true;
            IsProgressIndeterminate = true;
            StatusMessage = $"正在扫描 {SearchPath}...";
            DataItems.Clear();

            try
            {
                await Task.Run(PerformSearch);
            }
            catch (Exception ex)
            {
                StatusMessage = $"搜索出错: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
            }
        }

        private void PerformSearch()
        {
            try
            {
                if (!ValidateSearchPath())
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        StatusMessage = "搜索路径无效或无法访问。";
                    });
                    return;
                }

                var actualSearchPath = GetNormalizedSearchPath();
                var options = new QuickDataImportOptions
                {
                    IncludeFeatureClasses = IsFeatureClass,
                    IncludeTables = IsTable,
                    IncludeRasters = IsRaster,
                    IncludeLayerFiles = false,
                    Recurse = SearchSubfolders,
                    Keyword = Keyword?.Trim() ?? string.Empty
                };

                var importedNodes = _importService.ImportPaths(new[] { actualSearchPath }, options);
                var flattenedNodes = importedNodes
                    .SelectMany(FlattenForBatchList)
                    .OrderBy(node => node.Name)
                    .ToList();

                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    DataItems.Clear();
                    var index = 1;
                    foreach (var node in flattenedNodes)
                    {
                        DataItems.Add(ToDataItem(node, index++));
                    }

                    StatusMessage = flattenedNodes.Count > 0
                        ? $"搜索完成，找到 {flattenedNodes.Count} 个数据项。"
                        : "未找到匹配的数据项。";
                });
            }
            catch (Exception ex)
            {
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    StatusMessage = $"搜索出错: {ex.Message}";
                });
            }
        }

        private static DataItem ToDataItem(QuickDataNode node, int index)
        {
            return new DataItem
            {
                Index = index,
                Name = node.Name,
                DataType = GetDisplayDataType(node),
                CoordinateSystem = string.IsNullOrWhiteSpace(node.CoordinateSystem) ? "未知" : node.CoordinateSystem,
                FullPath = node.SourcePath,
                SourceNode = node,
                IsSelected = false
            };
        }

        private static string GetDisplayDataType(QuickDataNode node)
        {
            return node.NodeKind switch
            {
                QuickDataNodeKind.Table => "表",
                QuickDataNodeKind.Raster => "栅格",
                QuickDataNodeKind.Geodatabase => "地理数据库",
                QuickDataNodeKind.LayerFile => "图层文件",
                _ when node.GeometryKind == QuickDataGeometryKind.Point => "点要素",
                _ when node.GeometryKind == QuickDataGeometryKind.Polyline => "线要素",
                _ when node.GeometryKind == QuickDataGeometryKind.Polygon => "面要素",
                _ when node.GeometryKind == QuickDataGeometryKind.Multipoint => "多点要素",
                _ => "要素类"
            };
        }

        private static System.Collections.Generic.IEnumerable<QuickDataNode> FlattenForBatchList(QuickDataNode node)
        {
            if (node == null)
            {
                return Enumerable.Empty<QuickDataNode>();
            }

            return node.NodeKind == QuickDataNodeKind.Geodatabase
                ? node.Children
                : new[] { node };
        }

        private bool ValidateSearchPath()
        {
            return _searchPathValidator.IsAccessible(SearchPath);
        }

        private string GetNormalizedSearchPath()
        {
            if (string.IsNullOrWhiteSpace(SearchPath))
            {
                return string.Empty;
            }

            if ((SearchPath.Length == 3 && SearchPath.EndsWith(@":\")) || (SearchPath.Length == 2 && SearchPath.EndsWith(":")))
            {
                return SearchPath.EndsWith(@"\") ? SearchPath : SearchPath + @"\";
            }

            return SearchPath;
        }

        private void SelectAll()
        {
            foreach (var item in DataItems)
            {
                item.IsSelected = true;
            }
        }

        private void InvertSelection()
        {
            foreach (var item in DataItems)
            {
                item.IsSelected = !item.IsSelected;
            }
        }

        private bool CanAddData()
        {
            return DataItems.Any(item => item.IsSelected && item.SourceNode != null) && !IsProcessing;
        }

        private async void AddData()
        {
            if (!CanAddData())
            {
                return;
            }

            IsProcessing = true;
            StatusMessage = "正在添加数据到地图...";

            try
            {
                var selectedNodes = DataItems
                    .Where(item => item.IsSelected && item.SourceNode != null)
                    .Select(item => item.SourceNode.CloneDeep())
                    .ToList();

                var result = await _mapLoadService.LoadNodesToCurrentMapAsync(
                    selectedNodes,
                    string.IsNullOrWhiteSpace(GroupName) ? null : GroupName.Trim(),
                    preserveTypeBuckets: false);

                StatusMessage = result.MissingActiveMap
                    ? "没有活动地图。"
                    : $"添加完成，新增 {result.AddedCount}，跳过 {result.SkippedCount}，失败 {result.FailedCount}。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"添加数据出错: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void ShowHelp()
        {
            var helpText = string.Join(Environment.NewLine,
                "批量添加数据使用说明：",
                string.Empty,
                "1. 选择一个目录作为扫描根路径。",
                "2. 勾选要扫描的数据类型，可选择是否递归子目录。",
                "3. 点击“搜索”后，在列表里勾选要添加的数据。",
                "4. 如填写图层组名称，选中的数据会先进入同名组图层。",
                string.Empty,
                "支持：SHP、GDB 内要素类/表、常见栅格。",
                "提示：该工具用于临时扫描添加；长期收藏请使用“快捷添加数据”面板。");

            PresentationServices.Dialogs.Show(helpText, "帮助");
        }

        private void NotifyCanExecuteChanged()
        {
            if (_searchCommand is RelayCommand searchCommand)
            {
                searchCommand.RaiseCanExecuteChanged();
            }

            if (_addDataCommand is RelayCommand addDataCommand)
            {
                addDataCommand.RaiseCanExecuteChanged();
            }
        }
    }
}
