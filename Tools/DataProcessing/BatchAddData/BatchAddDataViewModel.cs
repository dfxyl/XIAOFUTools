using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XIAOFUTools.Common;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Catalog;

namespace XIAOFUTools.Tools.BatchAddData
{
    /// <summary>
    /// 数据项模型
    /// </summary>
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
    }

    /// <summary>
    /// 批量添加数据视图模型
    /// </summary>
    internal class BatchAddDataViewModel : PropertyChangedBase
    {
        #region 属性

        private string _searchPath = "";
        public string SearchPath
        {
            get => _searchPath;
            set
            {
                SetProperty(ref _searchPath, value);
                // 通知搜索命令状态可能已改变
                NotifyCanExecuteChanged();
            }
        }

        private bool _isFeatureClass = true;
        public bool IsFeatureClass
        {
            get => _isFeatureClass;
            set => SetProperty(ref _isFeatureClass, value);
        }

        private bool _isTable = true;
        public bool IsTable
        {
            get => _isTable;
            set => SetProperty(ref _isTable, value);
        }

        private bool _isRaster = true;
        public bool IsRaster
        {
            get => _isRaster;
            set => SetProperty(ref _isRaster, value);
        }

        private bool _searchSubfolders = true;
        public bool SearchSubfolders
        {
            get => _searchSubfolders;
            set => SetProperty(ref _searchSubfolders, value);
        }

        private string _keyword = "";
        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        private ObservableCollection<DataItem> _dataItems;
        public ObservableCollection<DataItem> DataItems
        {
            get => _dataItems;
            set => SetProperty(ref _dataItems, value);
        }

        private string _groupName = "";
        public string GroupName
        {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }

        private bool _isProcessing = false;
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

        private string _statusMessage = "请选择搜索路径并点击搜索按钮。";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private int _progress = 0;
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private bool _isProgressIndeterminate = false;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        #endregion

        #region 命令

        private ICommand _browseFolderCommand;
        public ICommand BrowseFolderCommand
        {
            get => _browseFolderCommand ?? (_browseFolderCommand = new RelayCommand(BrowseFolder));
        }

        private ICommand _searchCommand;
        public ICommand SearchCommand
        {
            get => _searchCommand ?? (_searchCommand = new RelayCommand(SearchData, () => CanSearch()));
        }

        private ICommand _selectAllCommand;
        public ICommand SelectAllCommand
        {
            get => _selectAllCommand ?? (_selectAllCommand = new RelayCommand(SelectAll));
        }

        private ICommand _invertSelectionCommand;
        public ICommand InvertSelectionCommand
        {
            get => _invertSelectionCommand ?? (_invertSelectionCommand = new RelayCommand(InvertSelection));
        }

        private ICommand _addDataCommand;
        public ICommand AddDataCommand
        {
            get => _addDataCommand ?? (_addDataCommand = new RelayCommand(AddData, CanAddData));
        }

        private ICommand _showHelpCommand;
        public ICommand ShowHelpCommand
        {
            get => _showHelpCommand ?? (_showHelpCommand = new RelayCommand(ShowHelp));
        }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public BatchAddDataViewModel()
        {
            DataItems = new ObservableCollection<DataItem>();

            // 设置默认搜索路径为当前项目文件夹
            try
            {
                var project = Project.Current;
                if (project != null)
                {
                    SearchPath = Path.GetDirectoryName(project.Path);
                }
            }
            catch
            {
                SearchPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        #region 私有方法

        /// <summary>
        /// 浏览文件夹
        /// </summary>
        private void BrowseFolder()
        {
            try
            {
                // 使用 ArcGIS Pro 自带的浏览对话框，过滤为“文件夹”
                var dlg = new OpenItemDialog
                {
                    Title = "选择要搜索的文件夹（支持选择驱动器根目录，如 E:\\）",
                    InitialLocation = string.IsNullOrEmpty(SearchPath) ? null : GetNormalizedSearchPath(),
                    MultiSelect = false,
                    Filter = ItemFilters.Folders
                };

                var result = dlg.ShowDialog();
                if (result == true && dlg.Items != null && dlg.Items.Count > 0)
                {
                    // 取首个选择项的路径
                    var item = dlg.Items[0];
                    if (item != null && !string.IsNullOrEmpty(item.Path))
                        SearchPath = item.Path;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"打开文件夹浏览器失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 是否可以搜索
        /// </summary>
        private bool CanSearch()
        {
            if (string.IsNullOrEmpty(SearchPath) || IsProcessing)
                return false;

            // 检查路径是否有效
            try
            {
                // 支持驱动器根目录（如 E:\ 或 E:）
                if ((SearchPath.Length == 3 && SearchPath.EndsWith(":\\")) ||
                    (SearchPath.Length == 2 && SearchPath.EndsWith(":")))
                {
                    // 标准化路径格式
                    string normalizedPath = SearchPath.EndsWith("\\") ? SearchPath : SearchPath + "\\";

                    // 检查驱动器是否存在且准备就绪
                    var drive = DriveInfo.GetDrives().FirstOrDefault(d =>
                        d.Name.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
                    return drive?.IsReady == true;
                }

                // 检查普通目录
                return Directory.Exists(SearchPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 搜索数据
        /// </summary>
        private async void SearchData()
        {
            if (!CanSearch())
            {
                StatusMessage = "搜索条件不满足，请检查路径是否有效";
                return;
            }

            IsProcessing = true;
            IsProgressIndeterminate = true;
            StatusMessage = $"正在搜索路径: {SearchPath}";
            DataItems.Clear();

            try
            {
                await Task.Run(() => PerformSearch());
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

        /// <summary>
        /// 执行搜索
        /// </summary>
        private void PerformSearch()
        {
            var foundItems = new List<DataItem>();
            int index = 1;

            try
            {
                // 验证搜索路径
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = "验证搜索路径...";
                });

                if (!ValidateSearchPath())
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = "搜索路径无效或无法访问";
                    });
                    return;
                }

                // 获取标准化的搜索路径
                string actualSearchPath = GetNormalizedSearchPath();

                // 搜索文件
                SearchOption searchOption = SearchSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"开始搜索 {actualSearchPath}... (搜索选项: {searchOption})";
                });

                // 测试路径访问
                try
                {
                    var testFiles = Directory.GetFiles(actualSearchPath, "*.*", SearchOption.TopDirectoryOnly);
                    var testDirs = Directory.GetDirectories(actualSearchPath, "*", SearchOption.TopDirectoryOnly);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"路径可访问: 找到 {testFiles.Length} 个文件, {testDirs.Length} 个文件夹";
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"路径访问测试失败: {ex.Message}";
                    });
                    return;
                }
                
                // 搜索Shapefile
                if (IsFeatureClass)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = "正在搜索Shapefile文件...";
                    });

                    var shpFiles = SafeEnumerateFiles(actualSearchPath, "*.shp", searchOption);
                    int shpCount = 0;

                    foreach (var file in shpFiles)
                    {
                        shpCount++;
                        if (MatchesKeyword(Path.GetFileNameWithoutExtension(file)))
                        {
                            string coordSys = GetCoordinateSystemFromShapefile(file);
                            string geometryType = GetGeometryTypeFromShapefile(file);
                            foundItems.Add(new DataItem
                            {
                                Index = index++,
                                Name = Path.GetFileNameWithoutExtension(file),
                                DataType = geometryType,
                                CoordinateSystem = coordSys,
                                FullPath = file,
                                IsSelected = false
                            });
                        }
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"找到 {shpCount} 个Shapefile文件";
                    });
                }

                // 搜索栅格文件
                if (IsRaster)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = "正在搜索栅格文件...";
                    });

                    var rasterExtensions = new[] { "*.tif", "*.tiff", "*.img", "*.jpg", "*.jpeg", "*.png", "*.bmp" };
                    int totalRasterFiles = 0;

                    foreach (var ext in rasterExtensions)
                    {
                        var files = SafeEnumerateFiles(actualSearchPath, ext, searchOption);
                        foreach (var file in files)
                        {
                            totalRasterFiles++;
                            if (MatchesKeyword(Path.GetFileNameWithoutExtension(file)))
                            {
                                string coordSys = GetCoordinateSystemFromRaster(file);
                                foundItems.Add(new DataItem
                                {
                                    Index = index++,
                                    Name = Path.GetFileNameWithoutExtension(file),
                                    DataType = "栅格",
                                    CoordinateSystem = coordSys,
                                    FullPath = file,
                                    IsSelected = false
                                });
                            }
                        }
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"找到 {totalRasterFiles} 个栅格文件";
                    });
                }

                // 搜索Geodatabase
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = "正在搜索地理数据库...";
                });

                var gdbDirs = SafeEnumerateDirectories(actualSearchPath, "*.gdb", searchOption);
                int gdbCount = 0;

                foreach (var gdbPath in gdbDirs)
                {
                    gdbCount++;
                    SearchGeodatabase(gdbPath, foundItems, ref index);
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"找到 {gdbCount} 个地理数据库";
                });

                // 在UI线程更新结果
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var item in foundItems)
                    {
                        DataItems.Add(item);
                    }
                    
                    StatusMessage = $"搜索完成，找到 {foundItems.Count} 个数据项。";
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"搜索出错: {ex.Message}";
                });
            }
        }

        /// <summary>
        /// 搜索Geodatabase
        /// </summary>
        private void SearchGeodatabase(string gdbPath, List<DataItem> foundItems, ref int index)
        {
            try
            {
                // 使用ArcGIS API搜索GDB内容
                using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath))))
                {
                    // 获取所有要素类
                    if (IsFeatureClass)
                    {
                        var featureClassDefinitions = geodatabase.GetDefinitions<FeatureClassDefinition>();
                        foreach (var fcDef in featureClassDefinitions)
                        {
                            if (MatchesKeyword(fcDef.GetName()))
                            {
                                string coordSys = "未知";
                                string geometryType = "要素类";
                                try
                                {
                                    var spatialRef = fcDef.GetSpatialReference();
                                    coordSys = spatialRef?.Name ?? "未知";

                                    // 获取几何类型
                                    geometryType = GetGeometryTypeFromDefinition(fcDef.GetShapeType());
                                }
                                catch { }

                                foundItems.Add(new DataItem
                                {
                                    Index = index++,
                                    Name = fcDef.GetName(),
                                    DataType = geometryType,
                                    CoordinateSystem = coordSys,
                                    FullPath = Path.Combine(gdbPath, fcDef.GetName()),
                                    IsSelected = false
                                });
                            }
                        }
                    }

                    // 获取所有表
                    if (IsTable)
                    {
                        var tableDefinitions = geodatabase.GetDefinitions<TableDefinition>();
                        foreach (var tableDef in tableDefinitions)
                        {
                            if (MatchesKeyword(tableDef.GetName()))
                            {
                                foundItems.Add(new DataItem
                                {
                                    Index = index++,
                                    Name = tableDef.GetName(),
                                    DataType = "表",
                                    CoordinateSystem = "无",
                                    FullPath = Path.Combine(gdbPath, tableDef.GetName()),
                                    IsSelected = false
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                // 如果无法打开GDB，则将整个GDB作为一个项目添加
                if (MatchesKeyword(Path.GetFileNameWithoutExtension(gdbPath)))
                {
                    foundItems.Add(new DataItem
                    {
                        Index = index++,
                        Name = Path.GetFileNameWithoutExtension(gdbPath),
                        DataType = "地理数据库",
                        CoordinateSystem = "未知",
                        FullPath = gdbPath,
                        IsSelected = false
                    });
                }

            }
        }

        /// <summary>
        /// 获取标准化的搜索路径
        /// </summary>
        private string GetNormalizedSearchPath()
        {
            if (string.IsNullOrEmpty(SearchPath))
                return SearchPath;

            // 处理驱动器根目录格式标准化
            if ((SearchPath.Length == 3 && SearchPath.EndsWith(":\\")) ||
                (SearchPath.Length == 2 && SearchPath.EndsWith(":")))
            {
                return SearchPath.EndsWith("\\") ? SearchPath : SearchPath + "\\";
            }

            return SearchPath;
        }

        /// <summary>
        /// 安全枚举文件，处理权限异常
        /// </summary>
        private IEnumerable<string> SafeEnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            try
            {
                if (searchOption == SearchOption.TopDirectoryOnly)
                {
                    // 只搜索顶级目录
                    return Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
                }
                else
                {
                    // 递归搜索，需要手动处理每个子目录的权限问题
                    return SafeEnumerateFilesRecursive(path, searchPattern);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 返回空枚举
                return Enumerable.Empty<string>();
            }
            catch (DirectoryNotFoundException)
            {
                return Enumerable.Empty<string>();
            }
            catch (Exception)
            {
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// 安全递归枚举文件
        /// </summary>
        private IEnumerable<string> SafeEnumerateFilesRecursive(string path, string searchPattern)
        {
            var result = new List<string>();

            // 首先枚举当前目录的文件
            try
            {
                var files = Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
                result.AddRange(files);
            }
            catch (UnauthorizedAccessException)
            {
                // 跳过无权限的目录
            }
            catch (Exception)
            {
                // 跳过其他异常
            }

            // 然后递归搜索子目录
            try
            {
                var directories = Directory.EnumerateDirectories(path);
                foreach (var directory in directories)
                {
                    result.AddRange(SafeEnumerateFilesRecursive(directory, searchPattern));
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 跳过无权限的目录
            }
            catch (Exception)
            {
                // 跳过其他异常
            }

            return result;
        }

        /// <summary>
        /// 安全枚举目录，处理权限异常
        /// </summary>
        private IEnumerable<string> SafeEnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            try
            {
                if (searchOption == SearchOption.TopDirectoryOnly)
                {
                    return Directory.EnumerateDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);
                }
                else
                {
                    return SafeEnumerateDirectoriesRecursive(path, searchPattern);
                }
            }
            catch (UnauthorizedAccessException)
            {
                return Enumerable.Empty<string>();
            }
            catch (DirectoryNotFoundException)
            {
                return Enumerable.Empty<string>();
            }
            catch (Exception)
            {
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// 安全递归枚举目录
        /// </summary>
        private IEnumerable<string> SafeEnumerateDirectoriesRecursive(string path, string searchPattern)
        {
            var result = new List<string>();

            // 首先枚举当前目录的匹配目录
            try
            {
                var matchingDirs = Directory.EnumerateDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);
                result.AddRange(matchingDirs);
            }
            catch (UnauthorizedAccessException)
            {
                // 跳过无权限的目录
            }
            catch (Exception)
            {
                // 跳过其他异常
            }

            // 然后递归搜索子目录
            try
            {
                var allDirs = Directory.EnumerateDirectories(path);
                foreach (var directory in allDirs)
                {
                    result.AddRange(SafeEnumerateDirectoriesRecursive(directory, searchPattern));
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 跳过无权限的目录
            }
            catch (Exception)
            {
                // 跳过其他异常
            }

            return result;
        }

        /// <summary>
        /// 验证搜索路径
        /// </summary>
        private bool ValidateSearchPath()
        {
            try
            {
                if (string.IsNullOrEmpty(SearchPath))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = "搜索路径为空";
                    });
                    return false;
                }

                // 处理驱动器根目录
                if ((SearchPath.Length == 3 && SearchPath.EndsWith(":\\")) ||
                    (SearchPath.Length == 2 && SearchPath.EndsWith(":")))
                {
                    // 标准化路径格式
                    string normalizedPath = SearchPath.EndsWith("\\") ? SearchPath : SearchPath + "\\";

                    // 检查驱动器是否存在且可访问
                    var drive = DriveInfo.GetDrives().FirstOrDefault(d =>
                        d.Name.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

                    if (drive == null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusMessage = $"驱动器 {normalizedPath} 不存在";
                        });
                        return false;
                    }

                    // 检查驱动器是否准备就绪
                    if (!drive.IsReady)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusMessage = $"驱动器 {normalizedPath} 未准备就绪 (类型: {drive.DriveType})";
                        });
                        return false;
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"驱动器 {normalizedPath} 验证成功 (类型: {drive.DriveType}, 可用空间: {drive.AvailableFreeSpace / (1024 * 1024 * 1024):F1}GB)";
                    });

                    // 不在这里更新SearchPath，避免在搜索过程中修改路径
                    // 返回true表示路径有效，搜索时使用normalizedPath

                    return true;
                }

                // 检查普通目录
                bool exists = Directory.Exists(SearchPath);
                if (!exists)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"目录 {SearchPath} 不存在";
                    });
                }
                return exists;
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"路径验证出错: {ex.Message}";
                });
                return false;
            }
        }

        /// <summary>
        /// 检查是否匹配关键字
        /// </summary>
        private bool MatchesKeyword(string name)
        {
            if (string.IsNullOrEmpty(Keyword))
                return true;

            return name.ToLower().Contains(Keyword.ToLower());
        }

        /// <summary>
        /// 全选
        /// </summary>
        private void SelectAll()
        {
            foreach (var item in DataItems)
            {
                item.IsSelected = true;
            }
        }

        /// <summary>
        /// 反选
        /// </summary>
        private void InvertSelection()
        {
            foreach (var item in DataItems)
            {
                item.IsSelected = !item.IsSelected;
            }
        }

        /// <summary>
        /// 是否可以添加数据
        /// </summary>
        private bool CanAddData()
        {
            return DataItems.Any(x => x.IsSelected) && !IsProcessing;
        }

        /// <summary>
        /// 添加数据到地图
        /// </summary>
        private async void AddData()
        {
            if (!CanAddData()) return;

            IsProcessing = true;
            StatusMessage = "正在添加数据到地图...";
            
            try
            {
                await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusMessage = "没有活动地图";
                        });
                        return;
                    }

                    GroupLayer groupLayer = null;
                    
                    // 如果指定了图层组名称，创建图层组
                    if (!string.IsNullOrEmpty(GroupName))
                    {
                        groupLayer = LayerFactory.Instance.CreateGroupLayer(map, 0, GroupName);
                    }

                    int addedCount = 0;
                    var selectedItems = DataItems.Where(x => x.IsSelected).ToList();
                    
                    foreach (var item in selectedItems)
                    {
                        try
                        {
                            // 根据数据类型添加到地图
                            if (item.DataType == "要素类" || item.DataType.Contains("点") || item.DataType.Contains("线") || item.DataType.Contains("面") || item.DataType.Contains("多点"))
                            {
                                Uri dataUri;

                                // 检查是否是GDB中的要素类
                                if (item.FullPath.Contains(".gdb") && !File.Exists(item.FullPath))
                                {
                                    // GDB中的要素类，构造正确的URI
                                    var gdbPath = item.FullPath.Substring(0, item.FullPath.IndexOf(".gdb") + 4);
                                    dataUri = new Uri($"{gdbPath}\\{item.Name}");
                                }
                                else
                                {
                                    // 独立的Shapefile
                                    dataUri = new Uri(item.FullPath);
                                }

                                var layerParams = new LayerCreationParams(dataUri);
                                layerParams.Name = item.Name;

                                if (groupLayer != null)
                                {
                                    LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, groupLayer);
                                }
                                else
                                {
                                    LayerFactory.Instance.CreateLayer<FeatureLayer>(layerParams, map);
                                }
                                addedCount++;
                            }
                            else if (item.DataType == "表")
                            {
                                Uri dataUri;

                                // 检查是否是GDB中的表
                                if (item.FullPath.Contains(".gdb") && !File.Exists(item.FullPath))
                                {
                                    // GDB中的表，构造正确的URI
                                    var gdbPath = item.FullPath.Substring(0, item.FullPath.IndexOf(".gdb") + 4);
                                    dataUri = new Uri($"{gdbPath}\\{item.Name}");
                                }
                                else
                                {
                                    // 独立的表文件（如DBF等）
                                    dataUri = new Uri(item.FullPath);
                                }

                                var tableParams = new StandaloneTableCreationParams(dataUri);
                                tableParams.Name = item.Name;

                                if (groupLayer != null)
                                {
                                    StandaloneTableFactory.Instance.CreateStandaloneTable(tableParams, groupLayer);
                                }
                                else
                                {
                                    StandaloneTableFactory.Instance.CreateStandaloneTable(tableParams, map);
                                }
                                addedCount++;
                            }
                            else if (item.DataType == "栅格" && File.Exists(item.FullPath))
                            {
                                var layerParams = new LayerCreationParams(new Uri(item.FullPath));
                                layerParams.Name = item.Name;

                                if (groupLayer != null)
                                {
                                    LayerFactory.Instance.CreateLayer<RasterLayer>(layerParams, groupLayer);
                                }
                                else
                                {
                                    LayerFactory.Instance.CreateLayer<RasterLayer>(layerParams, map);
                                }
                                addedCount++;
                            }
                            else if (item.DataType == "地理数据库" && Directory.Exists(item.FullPath))
                            {
                                // 对于整个GDB，跳过不添加
                                continue;
                            }
                        }
                        catch
                        {
                            // 静默处理错误，不中断批量添加过程
                        }
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"添加完成，成功添加 {addedCount} 个数据项。";
                    });
                });
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

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            // 添加调试信息
            var debugInfo = "";
            try
            {
                debugInfo += $"当前搜索路径: {SearchPath}\n";
                debugInfo += $"路径长度: {SearchPath?.Length}\n";
                debugInfo += $"是否为驱动器格式: {((SearchPath?.Length == 3 && SearchPath.EndsWith(":\\")) || (SearchPath?.Length == 2 && SearchPath.EndsWith(":")))}\n";

                if (!string.IsNullOrEmpty(SearchPath))
                {
                    debugInfo += $"Directory.Exists结果: {Directory.Exists(SearchPath)}\n";

                    // 检查驱动器信息
                    var drives = DriveInfo.GetDrives();
                    debugInfo += $"系统驱动器: {string.Join(", ", drives.Select(d => d.Name))}\n";

                    if ((SearchPath.Length == 3 && SearchPath.EndsWith(":\\")) ||
                        (SearchPath.Length == 2 && SearchPath.EndsWith(":")))
                    {
                        string normalizedPath = SearchPath.EndsWith("\\") ? SearchPath : SearchPath + "\\";
                        var targetDrive = drives.FirstOrDefault(d => d.Name.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
                        if (targetDrive != null)
                        {
                            debugInfo += $"目标驱动器信息: {targetDrive.Name}, 类型: {targetDrive.DriveType}, 就绪: {targetDrive.IsReady}\n";
                        }
                        else
                        {
                            debugInfo += $"未找到驱动器: {normalizedPath}\n";
                        }
                    }
                }

                debugInfo += $"搜索选项 - 要素类: {IsFeatureClass}, 表: {IsTable}, 栅格: {IsRaster}\n";
                debugInfo += $"搜索子文件夹: {SearchSubfolders}\n";
                debugInfo += $"关键字: '{Keyword}'\n";
            }
            catch (Exception ex)
            {
                debugInfo += $"调试信息获取出错: {ex.Message}\n";
            }

            var helpText = @"批量添加数据工具使用说明：

功能描述：
扫描指定文件夹中的GIS数据，并批量添加到当前地图中。

参数说明：
• 搜索路径：要扫描的文件夹路径
• 要素图层：包含Shapefile等矢量数据
• 表：包含数据表
• 影像栅格：包含TIFF、IMG等栅格数据
• 搜索子文件夹：是否递归搜索子文件夹
• 关键字：按名称过滤数据（空白表示搜索全部）
• 图层组名称：指定名称将创建图层组并将数据添加到组中

操作步骤：
1. 选择或输入搜索路径
2. 设置数据类型过滤条件
3. 输入关键字（可选）
4. 点击搜索按钮
5. 在结果列表中选择要添加的数据
6. 输入图层组名称（可选）
7. 点击添加数据按钮

注意事项：
• 支持Shapefile、栅格影像、地理数据库等格式
• 大量数据添加可能需要较长时间
• 建议先搜索确认数据后再添加

调试信息：
" + debugInfo;

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpText, "批量添加数据工具帮助");
        }

        /// <summary>
        /// 从Shapefile获取坐标系信息
        /// </summary>
        private string GetCoordinateSystemFromShapefile(string shpFilePath)
        {
            try
            {
                // 检查.prj文件是否存在
                string prjFilePath = Path.ChangeExtension(shpFilePath, ".prj");
                if (File.Exists(prjFilePath))
                {
                    string prjContent = File.ReadAllText(prjFilePath);
                    return ParseCoordinateSystemFromPrj(prjContent);
                }

                // 如果没有.prj文件，尝试使用ArcGIS API读取
                try
                {
                    using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(Path.GetDirectoryName(shpFilePath)))))
                    {
                        // 这里需要更复杂的逻辑来读取Shapefile的坐标系
                        // 暂时返回未知
                        return "未知";
                    }
                }
                catch
                {
                    return "未知";
                }
            }
            catch
            {
                return "未知";
            }
        }

        /// <summary>
        /// 从栅格文件获取坐标系信息
        /// </summary>
        private string GetCoordinateSystemFromRaster(string rasterFilePath)
        {
            try
            {
                // 检查.tfw, .tifw, .jgw等世界文件
                string[] worldFileExtensions = { ".tfw", ".tifw", ".tiffw", ".jgw", ".jpgw", ".jgpw", ".pgw", ".pngw", ".bpw", ".bmpw" };
                string baseName = Path.GetFileNameWithoutExtension(rasterFilePath);
                string directory = Path.GetDirectoryName(rasterFilePath);

                foreach (string ext in worldFileExtensions)
                {
                    string worldFilePath = Path.Combine(directory, baseName + ext);
                    if (File.Exists(worldFilePath))
                    {
                        // 找到世界文件，但世界文件本身不包含坐标系信息
                        // 检查是否有对应的.prj文件
                        string prjFilePath = Path.Combine(directory, baseName + ".prj");
                        if (File.Exists(prjFilePath))
                        {
                            string prjContent = File.ReadAllText(prjFilePath);
                            return ParseCoordinateSystemFromPrj(prjContent);
                        }
                        return "有世界文件";
                    }
                }

                // 对于TIFF文件，检查是否有内嵌的地理信息
                if (Path.GetExtension(rasterFilePath).ToLower() == ".tif" || Path.GetExtension(rasterFilePath).ToLower() == ".tiff")
                {
                    // 检查.prj文件
                    string prjFilePath = Path.ChangeExtension(rasterFilePath, ".prj");
                    if (File.Exists(prjFilePath))
                    {
                        string prjContent = File.ReadAllText(prjFilePath);
                        return ParseCoordinateSystemFromPrj(prjContent);
                    }
                    return "GeoTIFF";
                }

                return "未知";
            }
            catch
            {
                return "未知";
            }
        }

        /// <summary>
        /// 解析.prj文件内容获取坐标系名称
        /// </summary>
        private string ParseCoordinateSystemFromPrj(string prjContent)
        {
            try
            {
                if (string.IsNullOrEmpty(prjContent))
                    return "未知";

                // 简单解析WKT格式的坐标系定义
                prjContent = prjContent.Trim();

                // 查找坐标系名称
                if (prjContent.StartsWith("GEOGCS["))
                {
                    // 地理坐标系
                    int startIndex = prjContent.IndexOf("\"") + 1;
                    int endIndex = prjContent.IndexOf("\"", startIndex);
                    if (startIndex > 0 && endIndex > startIndex)
                    {
                        return prjContent.Substring(startIndex, endIndex - startIndex);
                    }
                }
                else if (prjContent.StartsWith("PROJCS["))
                {
                    // 投影坐标系
                    int startIndex = prjContent.IndexOf("\"") + 1;
                    int endIndex = prjContent.IndexOf("\"", startIndex);
                    if (startIndex > 0 && endIndex > startIndex)
                    {
                        return prjContent.Substring(startIndex, endIndex - startIndex);
                    }
                }

                // 常见坐标系的简单识别
                if (prjContent.Contains("WGS_1984"))
                    return "WGS 1984";
                if (prjContent.Contains("GCS_WGS_1984"))
                    return "WGS 1984";
                if (prjContent.Contains("Beijing_1954"))
                    return "Beijing 1954";
                if (prjContent.Contains("Xian_1980"))
                    return "Xian 1980";
                if (prjContent.Contains("CGCS2000"))
                    return "CGCS 2000";
                if (prjContent.Contains("UTM"))
                    return "UTM";
                if (prjContent.Contains("Web_Mercator"))
                    return "Web Mercator";

                return "已定义";
            }
            catch
            {
                return "未知";
            }
        }

        /// <summary>
        /// 从Shapefile获取几何类型
        /// </summary>
        private string GetGeometryTypeFromShapefile(string shpFilePath)
        {
            try
            {
                // 检查.shp文件的几何类型
                using (var fileStream = new FileStream(shpFilePath, FileMode.Open, FileAccess.Read))
                {
                    if (fileStream.Length < 100) return "要素类";

                    var buffer = new byte[100];
                    fileStream.Read(buffer, 0, 100);

                    // Shapefile头部的第32-35字节包含几何类型
                    if (buffer.Length >= 36)
                    {
                        int shapeType = BitConverter.ToInt32(buffer, 32);
                        return GetGeometryTypeFromShapeType(shapeType);
                    }
                }
            }
            catch
            {
                // 如果读取失败，返回默认值
            }

            return "要素类";
        }

        /// <summary>
        /// 从Shapefile几何类型代码获取类型名称
        /// </summary>
        private string GetGeometryTypeFromShapeType(int shapeType)
        {
            switch (shapeType)
            {
                case 1: return "点要素";
                case 3: return "线要素";
                case 5: return "面要素";
                case 8: return "多点要素";
                case 11: return "点要素(Z)";
                case 13: return "线要素(Z)";
                case 15: return "面要素(Z)";
                case 18: return "多点要素(Z)";
                case 21: return "点要素(M)";
                case 23: return "线要素(M)";
                case 25: return "面要素(M)";
                case 28: return "多点要素(M)";
                case 31: return "多面要素";
                default: return "要素类";
            }
        }

        /// <summary>
        /// 从ArcGIS几何类型定义获取类型名称
        /// </summary>
        private string GetGeometryTypeFromDefinition(GeometryType geometryType)
        {
            switch (geometryType)
            {
                case GeometryType.Point:
                    return "点要素";
                case GeometryType.Multipoint:
                    return "多点要素";
                case GeometryType.Polyline:
                    return "线要素";
                case GeometryType.Polygon:
                    return "面要素";
                case GeometryType.Multipatch:
                    return "多面要素";
                case GeometryType.Envelope:
                    return "范围要素";
                default:
                    return "要素类";
            }
        }

        /// <summary>
        /// 通知命令的CanExecute状态可能已改变
        /// </summary>
        private void NotifyCanExecuteChanged()
        {
            if (_searchCommand is RelayCommand searchCmd)
                searchCmd.RaiseCanExecuteChanged();
            if (_addDataCommand is RelayCommand addCmd)
                addCmd.RaiseCanExecuteChanged();
        }

        #endregion
    }
}
