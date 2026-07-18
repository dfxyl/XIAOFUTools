using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ExcelDataReader;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.ShapefileBuilder
{
    /// <summary>
    /// 要素集信息类（SHP模式下仅用于兼容模板校验）
    /// </summary>
    public class FeatureDatasetInfo
    {
        public int Index { get; set; }
        public string DatasetName { get; set; }
        public string DatasetAlias { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// 图层信息类
    /// </summary>
    public class LayerDefinition
    {
        public int Index { get; set; }
        public string LayerAlias { get; set; }
        public string GeometryType { get; set; }
        public string AttributesTable { get; set; }
        public string FeatureDataset { get; set; }
        public string Constraint { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// 字段信息类
    /// </summary>
    public class FieldDefinition
    {
        public int Index { get; set; }
        public string FieldAlias { get; set; }
        public string FieldCode { get; set; }
        public string FieldType { get; set; }
        public int? FieldLength { get; set; }
        public int? DecimalPlaces { get; set; }
        public string ValueRange { get; set; }
        public string Constraint { get; set; }
    }

    /// <summary>
    /// 属性表建SHP视图模型
    /// </summary>
    public partial class ShapefileBuilderViewModel : PropertyChangedBase
    {
        private readonly Infrastructure.ShapefileBuilderFileStore _fileStore = new Infrastructure.ShapefileBuilderFileStore();
        private string _inputExcelPath;
        private string _outputFolderPath;
        private bool _isProcessing;
        private string _logText;
        private SpatialReference _selectedSpatialReference;
        private string _selectedCoordinateSystemName;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ShapefileBuilderViewModel()
        {
            Initialize();
            InitializeCommands();
        }
    }
}
