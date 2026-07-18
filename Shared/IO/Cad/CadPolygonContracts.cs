using System.Collections.Generic;
using System.Drawing;

namespace XIAOFUTools.Shared.IO.Cad
{
    /// <summary>
    /// 脱离 ArcGIS 线程的二维 CAD 坐标快照。
    /// </summary>
    internal readonly record struct CadCoordinate(double X, double Y);

    /// <summary>
    /// 面要素导出的 CAD 几何、颜色与图层名快照，可由 DXF/DWG 写入器复用。
    /// </summary>
    internal sealed class CadPolygonSnapshot
    {
        internal List<List<CadCoordinate>> Rings { get; init; } = new();
        internal Color Color { get; init; }
        internal string LayerName { get; init; } = string.Empty;
    }

    internal sealed record CadPolygonReadOptions(
        bool UseFieldNaming,
        string FieldNamingSeparator,
        IReadOnlyList<string> NamingFields);
}
