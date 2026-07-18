using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using netDxf;
using netDxf.Entities;
using netDxf.Header;
using netDxf.Tables;
using XIAOFUTools.Features.Conversion.PolygonToDxfWithFill.Core;
using XIAOFUTools.Shared.IO.Cad;
using DxfLayer = netDxf.Tables.Layer;

namespace XIAOFUTools.Features.Conversion.PolygonToDxfWithFill.Infrastructure
{
    /// <summary>
    /// 封装 netDxf 文档构建与文件写入；输入为脱离 ArcGIS 线程的数据快照。
    /// </summary>
    internal sealed class DxfFillDocumentWriter
    {
        internal Task<DxfFillExportResult> WriteAsync(
            IReadOnlyList<CadPolygonSnapshot> polygons,
            string outputPath,
            DxfFillExportOptions options,
            Func<bool> isCancellationRequested,
            Action<DxfFillExportProgress> reportProgress)
        {
            ArgumentNullException.ThrowIfNull(polygons);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(isCancellationRequested);

            return Task.Run(() => Write(
                polygons,
                outputPath,
                options,
                isCancellationRequested,
                reportProgress));
        }

        private static DxfFillExportResult Write(
            IReadOnlyList<CadPolygonSnapshot> polygons,
            string outputPath,
            DxfFillExportOptions options,
            Func<bool> isCancellationRequested,
            Action<DxfFillExportProgress> reportProgress)
        {
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var document = new DxfDocument(ToDxfVersion(options.Version));
            var minimumX = double.PositiveInfinity;
            var minimumY = double.PositiveInfinity;
            var maximumX = double.NegativeInfinity;
            var maximumY = double.NegativeInfinity;
            var processedCount = 0;
            var cancelled = false;

            foreach (var polygon in polygons)
            {
                if (isCancellationRequested())
                {
                    cancelled = true;
                    break;
                }

                var layer = GetOrCreateLayer(document, polygon.LayerName, polygon.Color);
                var hatchPaths = new List<HatchBoundaryPath>();
                foreach (var ring in polygon.Rings)
                {
                    var vertices = EnumerateRingVertices(ring).ToArray();
                    if (vertices.Length < 3)
                    {
                        continue;
                    }

                    var boundary = new Polyline2D { IsClosed = true };
                    Polyline2D displayBoundary = null;
                    if (options.ExportBoundary)
                    {
                        displayBoundary = new Polyline2D
                        {
                            IsClosed = true,
                            Color = AciColor.ByLayer,
                            Layer = layer,
                            Lineweight = ToNearestLineweight(options.LineWidthMillimeters)
                        };
                    }

                    foreach (var point in vertices)
                    {
                        minimumX = Math.Min(minimumX, point.X);
                        minimumY = Math.Min(minimumY, point.Y);
                        maximumX = Math.Max(maximumX, point.X);
                        maximumY = Math.Max(maximumY, point.Y);
                        boundary.Vertexes.Add(new Polyline2DVertex(new Vector2(point.X, point.Y)));
                        displayBoundary?.Vertexes.Add(new Polyline2DVertex(new Vector2(point.X, point.Y)));
                    }

                    hatchPaths.Add(new HatchBoundaryPath(new List<EntityObject> { boundary }));
                    if (displayBoundary != null)
                    {
                        document.Entities.Add(displayBoundary);
                    }
                }

                if (options.ExportHatch && hatchPaths.Count > 0)
                {
                    var hatch = new Hatch(HatchPattern.Solid, hatchPaths, associative: false)
                    {
                        Color = AciColor.ByLayer,
                        Layer = layer,
                        Transparency = new Transparency((byte)Math.Clamp(
                            (int)Math.Round(options.HatchTransparency * 0.9),
                            0,
                            90))
                    };
                    document.Entities.Add(hatch);
                }

                processedCount++;
                if (processedCount % 10 == 0 || processedCount == polygons.Count)
                {
                    reportProgress?.Invoke(new DxfFillExportProgress(processedCount, polygons.Count));
                }
            }

            ConfigureActiveViewport(document, minimumX, minimumY, maximumX, maximumY);
            document.Save(outputPath);
            return new DxfFillExportResult(outputPath, processedCount, cancelled);
        }

        private static IEnumerable<CadCoordinate> EnumerateRingVertices(IReadOnlyList<CadCoordinate> ring)
        {
            if (ring == null || ring.Count == 0)
            {
                yield break;
            }

            var count = ring.Count;
            if (count > 1 && ring[0] == ring[^1])
            {
                count--;
            }

            for (var index = 0; index < count; index++)
            {
                yield return ring[index];
            }
        }

        private static void ConfigureActiveViewport(
            DxfDocument document,
            double minimumX,
            double minimumY,
            double maximumX,
            double maximumY)
        {
            if (double.IsPositiveInfinity(minimumX) || double.IsPositiveInfinity(minimumY))
            {
                return;
            }

            try
            {
                var viewport = document.VPorts["*Active"] ?? new VPort("*Active");
                if (!document.VPorts.Contains(viewport.Name))
                {
                    document.VPorts.Add(viewport);
                }

                viewport.ViewCenter = new Vector2((minimumX + maximumX) / 2, (minimumY + maximumY) / 2);
                viewport.ViewHeight = Math.Max(1.0, (maximumY - minimumY) * 1.05);
            }
            catch
            {
                // 视口写入失败不影响 DXF 文件生成。
            }
        }

        private static DxfLayer GetOrCreateLayer(DxfDocument document, string layerName, Color color)
        {
            var safeLayerName = SanitizeLayerName(layerName);
            DxfLayer layer = null;
            try
            {
                layer = document.Layers[safeLayerName];
            }
            catch
            {
                // 图层不存在时创建。
            }
            if (layer != null)
            {
                return layer;
            }

            layer = new DxfLayer(safeLayerName) { Color = ToAciColor(color) };
            document.Layers.Add(layer);
            return layer;
        }

        private static DxfVersion ToDxfVersion(DxfExportVersion version) => version switch
        {
            DxfExportVersion.AutoCad2013 => DxfVersion.AutoCad2013,
            DxfExportVersion.AutoCad2010 => DxfVersion.AutoCad2010,
            DxfExportVersion.AutoCad2007 => DxfVersion.AutoCad2007,
            DxfExportVersion.AutoCad2004 => DxfVersion.AutoCad2004,
            DxfExportVersion.AutoCad2000 => DxfVersion.AutoCad2000,
            _ => DxfVersion.AutoCad2018
        };

        private static Lineweight ToNearestLineweight(double millimeters)
        {
            var values = new[] { 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211 };
            var requested = (int)Math.Round(millimeters * 100.0);
            return (Lineweight)values.OrderBy(value => Math.Abs(value - requested)).First();
        }

        private static AciColor ToAciColor(Color color)
        {
            try
            {
                return new AciColor(color.R, color.G, color.B);
            }
            catch
            {
                return AciColor.ByLayer;
            }
        }

        private static string SanitizeLayerName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Layer0";
            }

            var result = value;
            foreach (var character in new[] { '<', '>', '/', '\\', ':', '"', '?', '*', '|', ',', ';', '=', '[', ']', '{', '}', '(', ')' })
            {
                result = result.Replace(character, '_');
            }

            result = result.Replace(' ', '_');
            return result.Length > 60 ? result[..60] : result;
        }
    }
}
