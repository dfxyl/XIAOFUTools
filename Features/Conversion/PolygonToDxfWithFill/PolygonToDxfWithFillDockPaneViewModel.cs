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

using XIAOFUTools.Features.Conversion.PolygonToDxfWithFill.Core;
using XIAOFUTools.Features.Conversion.PolygonToDxfWithFill.Infrastructure;
using XIAOFUTools.Shared.Gis;
using XIAOFUTools.Shared.IO.Cad;

namespace XIAOFUTools.Features.Conversion.PolygonToDxfWithFill
{
    // 组合键内部固定分隔符，避免依赖 SDK 的 FieldDelimiter 属性
    internal static class CompositeKey
    {
        public const string Delim = "\u001F"; // Unit Separator，不会出现在普通字段中
    }

    /// <summary>
    /// 面转DXF[带填充] DockPane视图模型
    /// </summary>
    internal partial class PolygonToDxfWithFillDockPaneViewModel : PropertyChangedBase, ICadFieldNamingTarget
    {
        private readonly DxfFillDocumentWriter _documentWriter = new();
        private readonly CadOutputPathResolver _outputPathResolver = new();
        private readonly ArcGisCadPolygonSnapshotReader _polygonReader = new();
        public PolygonToDxfWithFillDockPaneViewModel()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            NamingFields = new ObservableCollection<CadFieldOption>();

            RefreshLayersCommand = new RelayCommand(RefreshLayers);
            BrowseOutputPathCommand = new RelayCommand(BrowseOutputPath);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            CancelCommand = new RelayCommand(RequestCancel, () => IsProcessing);
            RunCommand = new RelayCommand(ExecuteAsyncSafe, () => CanProcess);

            ExportBoundary = true;
            ExportHatch = true;
            LineWidth = 0.25;           // mm
            HatchTransparency = 0;      // 0-100

            // 默认导出路径：桌面
            try
            {
                var name = SelectedPolygonLayer != null ? SanitizeFileName(SelectedPolygonLayer.Name) : "output";
                var defaultPath = _outputPathResolver.CreateDefaultOutputPath(name + ".dxf");
                if (!string.IsNullOrWhiteSpace(defaultPath))
                {
                    OutputPath = defaultPath;
                }
            }
            catch { }

            // 字段命名相关默认值
            UseFieldNaming = false;
            FieldNamingSeparator = "_";

            // DXF版本选项
            DxfVersions = new ObservableCollection<DxfVersionOption>(new[]
            {
                new DxfVersionOption { Name = "AutoCAD 2018 (R2018)", Version = DxfExportVersion.AutoCad2018 },
                new DxfVersionOption { Name = "AutoCAD 2013 (R2013)", Version = DxfExportVersion.AutoCad2013 },
                new DxfVersionOption { Name = "AutoCAD 2010 (R2010)", Version = DxfExportVersion.AutoCad2010 },
                new DxfVersionOption { Name = "AutoCAD 2007 (R2007)", Version = DxfExportVersion.AutoCad2007 },
                new DxfVersionOption { Name = "AutoCAD 2004 (R2004)", Version = DxfExportVersion.AutoCad2004 },
                new DxfVersionOption { Name = "AutoCAD 2000 (R2000)", Version = DxfExportVersion.AutoCad2000 },
            });
            SelectedDxfVersion = DxfVersions.FirstOrDefault();

            StatusMessage = "初始化完成";
            // 确保初始化后命令可用状态立刻刷新
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private bool _useFieldNaming;

        private string _fieldNamingSeparator;

        private FeatureLayer _selectedPolygonLayer;

        private string _outputPath;

        private DxfVersionOption _selectedDxfVersion;

        private bool _exportBoundary;

        private bool _exportHatch;

        private double _lineWidth;

        private int _hatchTransparency;

        private double _progress;

        private bool _isProgressIndeterminate;

        private string _statusMessage;

        private bool _isProcessing;

        private bool _cancelRequested;

        private string _logContent = string.Empty;
    }

    /// <summary>
    /// 简易RelayCommand（与DWG工具一致）
    /// </summary>


    public class DxfVersionOption
    {
        public string Name { get; set; }
        public DxfExportVersion Version { get; set; }
        public override string ToString() => Name;
    }
}
