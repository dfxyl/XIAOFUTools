using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO.Compression;
using System.Xml;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.Conversion.ExportToKml.Infrastructure;

namespace XIAOFUTools.Features.Conversion.ExportToKml
{
    /// <summary>
    /// 要素图层分组导出KML/KMZ视图模型
    /// </summary>
    internal partial class ExportToKmlViewModel : PropertyChangedBase
    {

        private const string NoGroupFieldOption = "不分组（直接导出）";
        private const string LayerToKmlToolName = "conversion.LayerToKML";
        private const string LayerToKmlLegacyToolName = "LayerToKML_conversion";
        private const string SelectLayerByAttributeToolName = "management.SelectLayerByAttribute";
        private const string SelectLayerByAttributeLegacyToolName = "SelectLayerByAttribute_management";
        private const string LabelStyleId = "xft_label_style";

        private CancellationTokenSource _cancellationTokenSource;
        private readonly KmlArchiveFileStore _fileStore = new();
        private bool _isProcessing = false;
        private double _progress = 0;
        private bool _isProgressIndeterminate = false;
        private string _statusMessage = "准备就绪";
        private string _logContent = "";

        private ObservableCollection<Layer> _featureLayers = new ObservableCollection<Layer>();
        private Layer _selectedInputLayer;

        private ObservableCollection<string> _groupFields = new ObservableCollection<string>();
        private string _selectedGroupField;

        private ObservableCollection<string> _exportFormats = new ObservableCollection<string>();
        private string _selectedExportFormat;

        private string _outputFolder = "";

        // 标注相关
        private bool _enableLabel = false;
        private ObservableCollection<string> _labelFields = new ObservableCollection<string>();
        private string _selectedLabelField;

        public ExportToKmlViewModel()
        {
            // 初始化集合
            FeatureLayers = new ObservableCollection<Layer>();
            GroupFields = new ObservableCollection<string>();
            LabelFields = new ObservableCollection<string>();
            ExportFormats = new ObservableCollection<string> { "KML", "KMZ" };

            // 初始化命令
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            RunCommand = new RelayCommand(async () => await RunExportAsync(), () => CanProcess);
            CancelCommand = new RelayCommand(CancelExport, () => IsProcessing);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            RefreshLayersCommand = new RelayCommand(RefreshLayers);

            // 默认选择 KML 格式
            SelectedExportFormat = "KML";

            // 加载图层
            LoadFeatureLayers();
        }
    }

    /// <summary>
    /// 分组导出项
    /// </summary>
    internal class GroupExportItem
    {
        public object RawValue { get; set; }
        public string DisplayValue { get; set; }
    }

    /// <summary>
    /// KML标注点项
    /// </summary>
    internal class KmlLabelItem
    {
        public string Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    /// <summary>
    /// RelayCommand实现
    /// </summary>

}
