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
using XIAOFUTools.Features.Cartography.ExportLayout.Infrastructure;


namespace XIAOFUTools.Features.Cartography.ExportLayout
{
    /// <summary>
    /// 导出布局视图模型
    /// </summary>
    public partial class ExportLayoutViewModel : INotifyPropertyChanged
    {
        private string _outputFolder = "";
        private string _resolution = "300";
        private string _selectedFormat = "PDF";
        private bool _isRunning = false;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly LayoutExportFileStore _fileStore = new();

        public ExportLayoutViewModel()
        {
            InitializeCommands();
            LoadLayouts();

            // 设置默认输出文件夹
            _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "布局导出");
        }
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

}
