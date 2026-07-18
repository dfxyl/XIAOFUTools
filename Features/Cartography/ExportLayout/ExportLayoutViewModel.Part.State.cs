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

namespace XIAOFUTools.Features.Cartography.ExportLayout
{
    public partial class ExportLayoutViewModel
    {

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

        public ICommand BrowseFolderCommand { get; private set; }
        public ICommand SelectAllCommand { get; private set; }
        public ICommand InvertSelectionCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand StartCommand { get; private set; }
        public ICommand RefreshLayoutsCommand { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
