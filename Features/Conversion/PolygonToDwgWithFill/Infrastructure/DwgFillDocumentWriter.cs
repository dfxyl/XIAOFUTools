using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using XIAOFUTools.Features.Conversion.PolygonToDwgWithFill.Core;
using XIAOFUTools.Shared.IO.Cad;
using CadLayer = ACadSharp.Tables.Layer;

namespace XIAOFUTools.Features.Conversion.PolygonToDwgWithFill.Infrastructure
{
    /// <summary>
    /// 封装 ACadSharp 的 DWG 边界、填充、视图和文件写入逻辑。
    /// </summary>
    internal sealed class DwgFillDocumentWriter
    {
        internal Task<DwgFillExportResult> WriteAsync(
            IReadOnlyList<CadPolygonSnapshot> polygons,
            string outputPath,
            DwgFillExportOptions options,
            Func<bool> isCancellationRequested,
            Action<DwgFillExportProgress> reportProgress)
        {
            ArgumentNullException.ThrowIfNull(polygons);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(isCancellationRequested);
            return Task.Run(() => Write(polygons, outputPath, options, isCancellationRequested, reportProgress));
        }

        private static DwgFillExportResult Write(
            IReadOnlyList<CadPolygonSnapshot> polygons,
            string outputPath,
            DwgFillExportOptions options,
            Func<bool> isCancellationRequested,
            Action<DwgFillExportProgress> reportProgress)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var document = new CadDocument();
            try
            {
                document.Header.Version = ToAcadVersion(options.Version);
            }
            catch
            {
                // 版本设置失败时保留库默认版本。
            }

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

                var targetLayer = GetOrCreateLayer(document, polygon.LayerName, polygon.Color);
                if (options.ExportBoundary)
                {
                    foreach (var ring in polygon.Rings)
                    {
                        var vertices = BuildVertices(ring);
                        UpdateExtents(vertices, ref minimumX, ref minimumY, ref maximumX, ref maximumY);
                        if (vertices.Count < 2)
                        {
                            continue;
                        }

                        var polyline = new Polyline2D(vertices, true)
                        {
                            Color = ToAcadColor(polygon.Color),
                            Layer = targetLayer
                        };
                        document.ModelSpace.Entities.Add(polyline);
                    }
                }

                if (options.ExportHatch)
                {
                    var alphaTransparency = 100 - (int)Math.Round(polygon.Color.A * 100.0 / 255.0);
                    var transparency = Math.Clamp(Math.Max(alphaTransparency, options.HatchTransparency), 0, 90);
                    var hatch = new Hatch
                    {
                        IsSolid = true,
                        IsAssociative = true,
                        Color = ACadSharp.Color.ByLayer,
                        Transparency = new Transparency((short)transparency),
                        Layer = targetLayer
                    };

                    foreach (var ring in polygon.Rings)
                    {
                        var vertices = BuildVertices(ring);
                        if (vertices.Count < 2)
                        {
                            continue;
                        }

                        UpdateExtents(vertices, ref minimumX, ref minimumY, ref maximumX, ref maximumY);
                        var boundary = new Polyline2D(vertices, true)
                        {
                            Color = ToAcadColor(polygon.Color),
                            Layer = targetLayer
                        };
                        document.ModelSpace.Entities.Add(boundary);
                        var path = new Hatch.BoundaryPath
                        {
                            Flags = BoundaryPathFlags.External | BoundaryPathFlags.Polyline
                        };
                        var edge = new Hatch.BoundaryPath.Polyline { IsClosed = true };
                        foreach (var vertex in vertices)
                        {
                            edge.Vertices.Add(new XYZ(vertex.Location.X, vertex.Location.Y, 0));
                        }
                        path.Edges.Add(edge);
                        path.Entities.Add(boundary);
                        hatch.Paths.Add(path);
                    }

                    document.ModelSpace.Entities.Add(hatch);
                }

                processedCount++;
                reportProgress?.Invoke(new DwgFillExportProgress(processedCount, polygons.Count));
            }

            ApplyInitialModelView(document, minimumX, minimumY, maximumX, maximumY);
            using var writer = new DwgWriter(outputPath, document);
            writer.Write();
            return new DwgFillExportResult(outputPath, processedCount, cancelled);
        }

        private static List<Vertex2D> BuildVertices(IReadOnlyList<CadCoordinate> ring)
        {
            var count = ring?.Count ?? 0;
            if (count > 1 && ring[0] == ring[^1])
            {
                count--;
            }

            var vertices = new List<Vertex2D>(count);
            for (var index = 0; index < count; index++)
            {
                vertices.Add(new Vertex2D(new XYZ(ring[index].X, ring[index].Y, 0)));
            }
            return vertices;
        }

        private static void UpdateExtents(
            IEnumerable<Vertex2D> vertices,
            ref double minimumX,
            ref double minimumY,
            ref double maximumX,
            ref double maximumY)
        {
            foreach (var vertex in vertices)
            {
                minimumX = Math.Min(minimumX, vertex.Location.X);
                minimumY = Math.Min(minimumY, vertex.Location.Y);
                maximumX = Math.Max(maximumX, vertex.Location.X);
                maximumY = Math.Max(maximumY, vertex.Location.Y);
            }
        }

        private static CadLayer GetOrCreateLayer(CadDocument document, string layerName, System.Drawing.Color color)
        {
            var safeLayerName = SanitizeLayerName(layerName);
            CadLayer layer = null;
            try { layer = document.Layers[safeLayerName]; } catch { }
            if (layer != null)
            {
                return layer;
            }

            layer = new CadLayer(safeLayerName)
            {
                Color = ToAcadColor(color)
            };
            document.Layers.Add(layer);
            return layer;
        }

        private static void ApplyInitialModelView(CadDocument document, double minimumX, double minimumY, double maximumX, double maximumY)
        {
            try
            {
                if (double.IsInfinity(minimumX) || double.IsInfinity(minimumY) || double.IsInfinity(maximumX) || double.IsInfinity(maximumY) ||
                    maximumX <= minimumX || maximumY <= minimumY)
                {
                    return;
                }

                var header = document.Header;
                header.ShowModelSpace = true;
                header.ModelSpaceExtMin = new XYZ(minimumX, minimumY, 0);
                header.ModelSpaceExtMax = new XYZ(maximumX, maximumY, 0);
                header.ModelSpaceLimitsMin = new XY(minimumX, minimumY);
                header.ModelSpaceLimitsMax = new XY(maximumX, maximumY);
                var width = maximumX - minimumX;
                var height = maximumY - minimumY;
                if (!document.VPorts.TryGetValue("*ACTIVE", out var viewport))
                {
                    viewport = new VPort("*ACTIVE");
                    document.VPorts.Add(viewport);
                }
                viewport.Center = new XY(minimumX + width / 2.0, minimumY + height / 2.0);
                viewport.ViewHeight = height;
                viewport.AspectRatio = width / height;
            }
            catch
            {
                // 视图范围失败不影响 DWG 写入。
            }
        }

        private static ACadVersion ToAcadVersion(DwgExportVersion version) => version switch
        {
            DwgExportVersion.AutoCad2013 => ACadVersion.AC1027,
            DwgExportVersion.AutoCad2010 => ACadVersion.AC1024,
            DwgExportVersion.AutoCad2007 => ACadVersion.AC1021,
            DwgExportVersion.AutoCad2004 => ACadVersion.AC1018,
            DwgExportVersion.AutoCad2000 => ACadVersion.AC1015,
            _ => ACadVersion.AC1032
        };

        private static ACadSharp.Color ToAcadColor(System.Drawing.Color color) =>
            new(color.R, color.G, color.B);

        private static string SanitizeLayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Layer0";
            var result = name;
            foreach (var character in new[] { '<', '>', '/', '\\', ':', '"', '?', '*', '|', ',', ';', '=', '[', ']', '{', '}', '(', ')' }) result = result.Replace(character, '_');
            result = result.Replace(' ', '_');
            return result.Length > 60 ? result[..60] : result;
        }
    }
}
