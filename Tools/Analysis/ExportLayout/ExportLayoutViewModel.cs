using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;


namespace XIAOFUTools.Tools.ExportLayout
{
    /// <summary>
    /// 导出布局视图模型
    /// </summary>
    public class ExportLayoutViewModel : INotifyPropertyChanged
    {
        private string _outputFolder = "";
        private string _resolution = "300";
        private string _selectedFormat = "PDF";
        private bool _isRunning = false;
        private CancellationTokenSource _cancellationTokenSource;

        public ExportLayoutViewModel()
        {
            InitializeCommands();
            LoadLayouts();
            
            // 设置默认输出文件夹
            _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "布局导出");
        }

        #region 属性

        /// <summary>
        /// 输出文件夹
        /// </summary>
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                _outputFolder = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 分辨率
        /// </summary>
        public string Resolution
        {
            get => _resolution;
            set
            {
                _resolution = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 选中的导出格式
        /// </summary>
        public string SelectedFormat
        {
            get => _selectedFormat;
            set
            {
                _selectedFormat = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 导出格式列表
        /// </summary>
        public ObservableCollection<string> ExportFormats { get; } = new ObservableCollection<string>
        {
            "PDF", "TIF", "JPG", "PNG", "GeoTIFF"
        };

        /// <summary>
        /// 布局列表
        /// </summary>
        public ObservableCollection<LayoutItem> Layouts { get; } = new ObservableCollection<LayoutItem>();

        #endregion

        #region 命令

        public ICommand BrowseFolderCommand { get; private set; }
        public ICommand SelectAllCommand { get; private set; }
        public ICommand InvertSelectionCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand StartCommand { get; private set; }
        public ICommand RefreshLayoutsCommand { get; private set; }

        private void InitializeCommands()
        {
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            SelectAllCommand = new RelayCommand(SelectAll);
            InvertSelectionCommand = new RelayCommand(InvertSelection);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            StopCommand = new RelayCommand(Stop);
            StartCommand = new RelayCommand(async () => await StartExport(), CanStart);
            RefreshLayoutsCommand = new RelayCommand(RefreshLayouts);
        }

        #endregion

        #region 方法

        /// <summary>
        /// 加载布局列表
        /// </summary>
        private async void LoadLayouts()
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var project = Project.Current;
                    if (project == null) return;

                    var layouts = project.GetItems<LayoutProjectItem>();
                    
                    foreach (var layout in layouts)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            Layouts.Add(new LayoutItem { Name = layout.Name, IsSelected = true });
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新布局列表
        /// </summary>
        public void RefreshLayouts()
        {
            Layouts.Clear();
            LoadLayouts();
        }

        /// <summary>
        /// 浏览文件夹
        /// </summary>
        private void BrowseFolder()
        {
            var dialog = new OpenFolderDialog()
            {
                Title = "选择输出文件夹",
                InitialDirectory = OutputFolder
            };

            if (dialog.ShowDialog() == true)
            {
                OutputFolder = dialog.FolderName;
            }
        }

        /// <summary>
        /// 全选
        /// </summary>
        private void SelectAll()
        {
            foreach (var layout in Layouts)
            {
                layout.IsSelected = true;
            }
        }

        /// <summary>
        /// 反选
        /// </summary>
        private void InvertSelection()
        {
            foreach (var layout in Layouts)
            {
                layout.IsSelected = !layout.IsSelected;
            }
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "导出布局工具帮助\n\n" +
                "功能描述：\n" +
                "批量导出项目中的布局为指定格式的文件。\n\n" +
                "参数说明：\n" +
                "- 输出文件夹：导出文件的保存位置\n" +
                "- 分辨率：导出图像的分辨率，单位为DPI，默认300\n" +
                "- 导出格式：支持PDF、TIF、JPG、PNG、GeoTIFF等格式\n" +
                "- 布局列表：显示项目中所有可用的布局，可选择需要导出的布局\n\n" +
                "操作步骤：\n" +
                "1. 选择输出文件夹\n" +
                "2. 设置分辨率（建议300DPI）\n" +
                "3. 选择导出格式\n" +
                "4. 在布局列表中选择要导出的布局\n" +
                "5. 点击\"开始\"按钮执行导出\n\n" +
                "注意事项：\n" +
                "- 确保输出文件夹有写入权限\n" +
                "- 导出过程中请勿关闭ArcGIS Pro\n" +
                "- 大量布局导出可能需要较长时间\n" +
                "- GeoTIFF格式会包含完整的地理参考信息和坐标系统";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpContent, "导出布局工具帮助");
        }

        /// <summary>
        /// 停止导出
        /// </summary>
        private void Stop()
        {
            _cancellationTokenSource?.Cancel();
            IsRunning = false;
        }

        /// <summary>
        /// 是否可以开始导出
        /// </summary>
        private bool CanStart()
        {
            return !IsRunning && 
                   !string.IsNullOrWhiteSpace(OutputFolder) && 
                   Layouts.Any(l => l.IsSelected) &&
                   int.TryParse(Resolution, out int res) && res > 0;
        }

        /// <summary>
        /// 开始导出
        /// </summary>
        private async Task StartExport()
        {
            try
            {
                IsRunning = true;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                // 创建输出文件夹
                if (!Directory.Exists(OutputFolder))
                {
                    Directory.CreateDirectory(OutputFolder);
                }

                var selectedLayouts = Layouts.Where(l => l.IsSelected).ToList();
                int totalCount = selectedLayouts.Count;
                int currentCount = 0;

                foreach (var layoutItem in selectedLayouts)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    currentCount++;
                    
                    await QueuedTask.Run(() =>
                    {
                        try
                        {
                            ExportLayout(layoutItem.Name);
                        }
                        catch (Exception ex)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"导出布局 '{layoutItem.Name}' 时发生错误：{ex.Message}",
                                              "导出错误");
                            });
                        }
                    });
                }

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"导出完成！共导出 {currentCount} 个布局。", "导出完成");
                }
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"导出过程中发生错误：{ex.Message}", "错误");
            }
            finally
            {
                IsRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 导出单个布局
        /// </summary>
        private void ExportLayout(string layoutName)
        {
            var project = Project.Current;
            var layoutItem = project.GetItems<LayoutProjectItem>().FirstOrDefault(l => l.Name == layoutName);
            
            if (layoutItem == null) return;

            var layout = layoutItem.GetLayout();
            if (layout == null) return;

            string fileName = $"{layoutName}.{GetFileExtension()}";
            string filePath = Path.Combine(OutputFolder, fileName);

            // 解析分辨率
            if (!int.TryParse(Resolution, out int resolution))
                resolution = 300;

            // 根据格式导出
            switch (SelectedFormat.ToUpper())
            {
                case "PDF":
                    ExportToPDF(layout, filePath, resolution);
                    break;
                case "TIF":
                case "TIFF":
                    ExportToTIFF(layout, filePath, resolution, false);
                    break;
                case "GEOTIFF":
                    ExportToGeoTIFF(layout, filePath, resolution);
                    break;
                case "JPG":
                case "JPEG":
                    ExportToJPEG(layout, filePath, resolution);
                    break;
                case "PNG":
                    ExportToPNG(layout, filePath, resolution);
                    break;
            }
        }

        /// <summary>
        /// 获取文件扩展名
        /// </summary>
        private string GetFileExtension()
        {
            return SelectedFormat.ToLower() switch
            {
                "pdf" => "pdf",
                "tif" => "tif",
                "tiff" => "tif",
                "geotiff" => "tif",
                "jpg" => "jpg",
                "jpeg" => "jpg",
                "png" => "png",
                _ => "pdf"
            };
        }

        /// <summary>
        /// 导出为PDF
        /// </summary>
        private void ExportToPDF(Layout layout, string filePath, int resolution)
        {
            var exportFormat = new PDFFormat()
            {
                Resolution = resolution,
                OutputFileName = filePath
            };
            layout.Export(exportFormat);
        }

        /// <summary>
        /// 导出为TIFF
        /// </summary>
        private void ExportToTIFF(Layout layout, string filePath, int resolution, bool withWorldFile)
        {
            var exportFormat = new TIFFFormat()
            {
                Resolution = resolution,
                OutputFileName = filePath,
                HasWorldFile = withWorldFile
            };
            layout.Export(exportFormat);
        }

        /// <summary>
        /// 导出为GeoTIFF（带地理参考信息）
        /// </summary>
        private void ExportToGeoTIFF(Layout layout, string filePath, int resolution)
        {
            // 获取布局中的地图框架
            string mapFrameName = GetValidMapFrameName(layout);

            if (string.IsNullOrEmpty(mapFrameName))
            {
                // 如果没有找到有效的地图框架，回退到普通TIFF导出
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    "布局中没有找到有效的地图框架，将导出为普通TIFF格式。\n" +
                    "要导出GeoTIFF，请确保布局中包含至少一个地图框架且该框架有有效的坐标系统。",
                    "地理参考警告");
                ExportToTIFF(layout, filePath, resolution, true);
                return;
            }

            // 创建TIFF导出格式，指定地图框架作为地理参考源
            var exportFormat = new TIFFFormat()
            {
                Resolution = resolution,
                OutputFileName = filePath,
                HasWorldFile = true,                    // 生成世界文件(.tfw)
                HasGeoTiffTags = true,                  // 嵌入GeoTIFF标签到TIFF文件中
                ImageCompressionQuality = 100,          // 最高质量，确保精度
                GeoReferenceMapFrameName = mapFrameName // 指定用于地理参考的地图框架
            };

            layout.Export(exportFormat);
        }

        /// <summary>
        /// 获取布局中第一个有效的地图框架名称
        /// </summary>
        private string GetValidMapFrameName(Layout layout)
        {
            try
            {
                // 获取布局中的所有元素，然后筛选出地图框架
                var allElements = layout.GetElements();
                var mapFrames = allElements.OfType<MapFrame>().ToList();

                System.Diagnostics.Debug.WriteLine($"布局中找到 {mapFrames.Count} 个地图框架");

                foreach (var mapFrame in mapFrames)
                {
                    System.Diagnostics.Debug.WriteLine($"检查地图框架: {mapFrame.Name}");

                    // 检查地图框架是否有有效的地图
                    if (mapFrame.Map == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  地图框架 {mapFrame.Name} 没有关联地图");
                        continue;
                    }

                    // 检查地图是否有坐标系统
                    if (mapFrame.Map.SpatialReference == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  地图框架 {mapFrame.Name} 的地图没有坐标系统");
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"  地图框架 {mapFrame.Name} 有效，坐标系统: {mapFrame.Map.SpatialReference.Name}");
                    return mapFrame.Name;
                }

                System.Diagnostics.Debug.WriteLine("没有找到有效的地图框架");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取地图框架时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 导出为JPEG
        /// </summary>
        private void ExportToJPEG(Layout layout, string filePath, int resolution)
        {
            var exportFormat = new JPEGFormat()
            {
                Resolution = resolution,
                OutputFileName = filePath
            };
            layout.Export(exportFormat);
        }

        /// <summary>
        /// 导出为PNG
        /// </summary>
        private void ExportToPNG(Layout layout, string filePath, int resolution)
        {
            var exportFormat = new PNGFormat()
            {
                Resolution = resolution,
                OutputFileName = filePath
            };
            layout.Export(exportFormat);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// 布局项
    /// </summary>
    public class LayoutItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 简单的命令实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }
}
