using System;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport.Core
{
    public sealed class CoordinateTableSettings
    {
        public bool EnableCoordinateTable { get; set; }
        public string LayerName { get; set; } = string.Empty;
        public string UniqueField { get; set; } = string.Empty;
        public string TitleText { get; set; } = "2000国家大地坐标系111°";
        public string PointPrefix { get; set; } = "J";
        public int XYDecimal { get; set; } = 3;
        public bool GenerateEdge { get; set; } = true;
        public int EdgeDecimal { get; set; } = 2;
        public bool UseAnchorPosition { get; set; }
        public string MapFrameName { get; set; } = string.Empty;
        public string AnchorElementName { get; set; } = "XF_MD";
        public double PointColWidth { get; set; } = 10;
        public double XYColWidth { get; set; } = 22;
        public double EdgeColWidth { get; set; } = 12;
        public double RowHeight { get; set; } = 5.5;
        public string PlacementCorner { get; set; } = "左下角";
        public double CornerOffset { get; set; } = 2;
        public string AreaMode { get; set; } = "自动生成";
        public string CustomAreaText { get; set; } = "S=[Shape_Area] 平方米";
        public string AreaUnit { get; set; } = "平方米";
        public int AreaDecimal { get; set; } = 2;
        public int MuDecimal { get; set; } = 4;
        public int RowsPerColumn { get; set; } = 20;
        public int CompressTotalRows { get; set; }
        public bool SwapXY { get; set; } = true;
        public int ExportDelay { get; set; } = 1000;
        public bool EnableBoundaryPoints { get; set; }
        public double BoundaryPointSize { get; set; } = 8.0;
        public bool EnablePointLabels { get; set; }
        public string PointLabelPrefix { get; set; } = "J";
        public string PointLabelSuffix { get; set; } = string.Empty;
        public double PointLabelDistance { get; set; } = 3.0;
        public double PointLabelSize { get; set; } = 12.0;
        public string PointLabelOverlapMode { get; set; } = "压盖隐藏";
        public bool EnableEdgeLabels { get; set; }
        public string EdgeLabelPrefix { get; set; } = string.Empty;
        public string EdgeLabelSuffix { get; set; } = string.Empty;
        public int EdgeLabelDecimal { get; set; } = 2;
        public bool EdgeLabelPadZeros { get; set; }
        public double EdgeLabelDistance { get; set; } = 2.0;
        public double EdgeLabelSize { get; set; } = 10.0;
        public string EdgeLabelOverlapMode { get; set; } = "压盖隐藏";
        public bool EnableIntersectTable { get; set; }
        public string IntersectLayerName { get; set; } = string.Empty;
        public string IntersectClassField { get; set; } = string.Empty;
        public string IntersectTableTitle { get; set; } = "交集汇总表";
        public string IntersectAreaUnit { get; set; } = "平方米";
        public int IntersectDecimalPlaces { get; set; } = 2;
        public double IntersectCategoryColWidth { get; set; } = 22;
        public double IntersectAreaColWidth { get; set; } = 18;
        public double IntersectRowHeight { get; set; } = 5.5;
        public string IntersectPlacementCorner { get; set; } = "右下角";
        public double IntersectCornerOffset { get; set; } = 2;

        public string Title { get => TitleText; set => TitleText = value; }
        public string Prefix { get => PointPrefix; set => PointPrefix = value; }
        public int DecimalPlaces { get => XYDecimal; set => XYDecimal = value; }

        public string FormatPointLabel(int index)
        {
            return $"{PointLabelPrefix}{index}{PointLabelSuffix}";
        }

        public string FormatEdgeLabel(double length)
        {
            var number = EdgeLabelPadZeros
                ? length.ToString($"F{EdgeLabelDecimal}")
                : Math.Round(length, EdgeLabelDecimal).ToString();
            return $"{EdgeLabelPrefix}{number}{EdgeLabelSuffix}";
        }
    }
}
