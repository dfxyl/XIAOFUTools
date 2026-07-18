using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;

using XIAOFUTools.Features.Conversion.PolygonToDwgWithFill.Core;
using XIAOFUTools.Features.Conversion.PolygonToDwgWithFill.Infrastructure;
using XIAOFUTools.Shared.Gis;
using XIAOFUTools.Shared.IO.Cad;

namespace XIAOFUTools.Features.Conversion.PolygonToDwgWithFill
{
    // 组合键内部固定分隔符，保证多字段值唯一
    internal static class CompositeKey
    {
        public const string Delim = "\u001F"; // Unit Separator
    }

    /// <summary>
    /// 面要素图层转DWG[带色块填充] DockPane视图模型
    /// </summary>
    internal partial class PolygonToDwgWithFillDockPaneViewModel : PropertyChangedBase, ICadFieldNamingTarget
    {
        private readonly DwgFillDocumentWriter _documentWriter = new();
        private readonly CadOutputPathResolver _outputPathResolver = new();
        private readonly ArcGisCadPolygonSnapshotReader _polygonReader = new();
        public PolygonToDwgWithFillDockPaneViewModel()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();

            RefreshLayersCommand = new RelayCommand(RefreshLayers);
            BrowseOutputPathCommand = new RelayCommand(BrowseOutputPath);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            CancelCommand = new RelayCommand(RequestCancel, () => IsProcessing);
            RunCommand = new RelayCommand(ExecuteAsyncSafe, () => CanProcess);

            ExportBoundary = true;
            ExportHatch = true;
            LineWidth = 0.25;
            HatchTransparency = 0; // 0-100

            // 图层命名：默认关闭按字段命名，初始化集合
            UseFieldNaming = false;
            FieldNamingSeparator = "_";
            NamingFields = new ObservableCollection<CadFieldOption>();

            // 默认导出路径：桌面
            try
            {
                OutputPath = _outputPathResolver.CreateDefaultOutputPath("output.dwg");
            }
            catch { /* 容错 */ }

            // 初始化 DWG 版本选项
            DwgVersions = new ObservableCollection<DwgVersionOption>(new[]
            {
                new DwgVersionOption { Name = "AutoCAD 2018 (AC1032)", Version = DwgExportVersion.AutoCad2018 },
                new DwgVersionOption { Name = "AutoCAD 2013 (AC1027)", Version = DwgExportVersion.AutoCad2013 },
                new DwgVersionOption { Name = "AutoCAD 2010 (AC1024)", Version = DwgExportVersion.AutoCad2010 },
                new DwgVersionOption { Name = "AutoCAD 2007 (AC1021)", Version = DwgExportVersion.AutoCad2007 },
                new DwgVersionOption { Name = "AutoCAD 2004 (AC1018)", Version = DwgExportVersion.AutoCad2004 },
                new DwgVersionOption { Name = "AutoCAD 2000 (AC1015)", Version = DwgExportVersion.AutoCad2000 },
            });
            SelectedDwgVersion = DwgVersions.FirstOrDefault();

            StatusMessage = "初始化完成";
        }

        private FeatureLayer _selectedPolygonLayer;

        private string _outputPath;

        private DwgVersionOption _selectedDwgVersion;

        private bool _exportBoundary;

        private bool _exportHatch;

        private double _lineWidth;

        private int _hatchTransparency;

        // 图层命名：是否按字段命名
        private bool _useFieldNaming;

        // 图层命名：分隔符
        private string _fieldNamingSeparator;

        private int _progress;

        private bool _isProgressIndeterminate;

        private bool _isProcessing;

        private bool _cancelRequested;

        private string _logContent = string.Empty;

        private string _statusMessage = string.Empty;
    }

    /// <summary>
    /// 简易RelayCommand
    /// </summary>

}
