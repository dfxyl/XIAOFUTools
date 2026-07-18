using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Text;
using System.Globalization;

namespace XIAOFUTools.Features.General.DownloadOnlineImagery
{
    /// <summary>
    /// 下载级别项
    /// </summary>
    public class DownloadLevelItem
    {
        public string Level { get; set; }
        public string Description { get; set; }
        public int Scale { get; set; }

        public override string ToString()
        {
            return $"{Level}级 (1:{Scale:N0})";
        }
    }

    /// <summary>
    /// 下载在线影像视图模型
    /// </summary>
    internal partial class DownloadOnlineImageryViewModel : INotifyPropertyChanged
    {
        private readonly Infrastructure.OnlineImageryFileLifecycleService _fileLifecycle = new();

        private ObservableCollection<Layer> _featureLayers;

        private Layer _selectedFeatureLayer;

        private string _outputFolder = "";

        private ObservableCollection<DownloadLevelItem> _downloadLevels;

        private DownloadLevelItem _selectedDownloadLevel;

        private bool _mergeImages = true;

        private bool _isProcessing = false;

        private int _progress = 0;

        private bool _isProgressIndeterminate = false;

        private string _logMessages = "";

        private ICommand _browseOutputFolderCommand;

        private ICommand _startDownloadCommand;

        private ICommand _showHelpCommand;

        private ICommand _stopDownloadCommand;

        private ICommand _refreshLayersCommand;

        /// <summary>
        /// 构造函数
        /// </summary>
        public DownloadOnlineImageryViewModel()
        {
            try
            {
                AddLogMessage("正在初始化ViewModel...");
                InitializeData();

                // 延迟加载图层，避免在构造函数中执行异步操作
                PresentationServices.UiThread.PostLoaded(LoadFeatureLayers);

                AddLogMessage("ViewModel初始化完成。");
            }
            catch (Exception ex)
            {
                AddLogMessage($"ViewModel初始化失败: {ex.Message}");
            }
        }

        private System.Threading.CancellationTokenSource _cancellationTokenSource;

        private List<string> _exportedImageFiles = new List<string>();
    }

    /// <summary>
    /// RelayCommand实现
    /// </summary>

}
