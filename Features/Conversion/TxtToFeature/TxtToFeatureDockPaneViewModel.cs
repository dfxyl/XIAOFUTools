using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.TxtToFeature
{
    /// <summary>
    /// TXT转SHP停靠窗格视图模型
    /// </summary>
    public partial class TxtToFeatureDockPaneViewModel : INotifyPropertyChanged
    {
        private readonly Infrastructure.TxtPlotFileReader _plotFileReader = new();
        private readonly Infrastructure.TxtToFeatureFileStore _fileStore = new();

        private string _inputFolder = "";
        private string _outputFolder = "";
        private string _fieldNames = "界址点数,地块面积,地块编号,地块名称,图形类型,图幅号,地块用途,地类编码,描述,@";
        private bool _separateFolder = false;
        private bool _mergeToOneFile = false;
        private bool _swapXY = false;
        private bool _saveToSourcePath = true;
        private bool _includeSubfolders = true;
        private SpatialReference _selectedSpatialReference;
        private string _selectedCoordinateSystemName = "未选择坐标系";
        private bool _isProcessing = false;
        private double _progress = 0;
        private bool _isProgressIndeterminate = false;
        private string _logText = "";
        private string _statusText = "就绪";
        private bool _cancelRequested = false;

        public TxtToFeatureDockPaneViewModel()
        {
            SelectInputFolderCommand = new RelayCommand(SelectInputFolder);
            SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder);
            SelectCoordinateSystemCommand = new RelayCommand(SelectCoordinateSystem);
            StartConversionCommand = new RelayCommand(async () => await StartConversionAsync(), () => !IsProcessing);
            StopConversionCommand = new RelayCommand(StopConversion, () => IsProcessing);
            HelpCommand = new RelayCommand(ShowHelp);
        }
    }

    /// <summary>
    /// 简单的RelayCommand实现
    /// </summary>


    /// <summary>
    /// 地块数据
    /// </summary>
    public class PlotData
    {
        public Dictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
        public List<CoordinateRing> Rings { get; set; } = new List<CoordinateRing>();
    }

    /// <summary>
    /// 坐标环
    /// </summary>
    public class CoordinateRing
    {
        public int RingNumber { get; set; }
        public List<CoordinatePoint> Points { get; set; } = new List<CoordinatePoint>();
    }

    /// <summary>
    /// 坐标点
    /// </summary>
    public class CoordinatePoint
    {
        public string PointName { get; set; }
        public int RingNumber { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
