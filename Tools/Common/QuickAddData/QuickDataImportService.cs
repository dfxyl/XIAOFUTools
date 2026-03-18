#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Tools.QuickAddData
{
    public sealed class QuickDataImportService : IQuickDataLegacyHydrationImportService
    {
        private static readonly HashSet<string> RasterExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".tif", ".tiff", ".img", ".jpg", ".jpeg", ".png", ".bmp"
        };

        private static readonly HashSet<string> LayerFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".lyr", ".lyrx"
        };

        private readonly IQuickDataGeodatabaseInspector _geodatabaseInspector;

        public QuickDataImportService(IQuickDataGeodatabaseInspector geodatabaseInspector)
        {
            _geodatabaseInspector = geodatabaseInspector ?? new NullQuickDataGeodatabaseInspector();
        }

        public IReadOnlyList<QuickDataNode> ImportPaths(IEnumerable<string> inputPaths, QuickDataImportOptions? options = null)
        {
            var importOptions = options ?? new QuickDataImportOptions();
            var results = new List<QuickDataNode>();
            var detector = new QuickDataDuplicateDetector(Array.Empty<string>());

            foreach (var inputPath in inputPaths ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(inputPath))
                {
                    continue;
                }

                ImportPath(inputPath, importOptions, results, detector);
            }

            return results;
        }

        public QuickDataNode? ImportGeodatabase(string geodatabasePath)
        {
            return CreateGeodatabaseNode(geodatabasePath, new QuickDataImportOptions
            {
                IncludeFeatureClasses = true,
                IncludeTables = true,
                IncludeRasters = false,
                IncludeLayerFiles = false,
                Recurse = false
            });
        }

        private void ImportPath(string inputPath, QuickDataImportOptions options, ICollection<QuickDataNode> results, QuickDataDuplicateDetector detector)
        {
            if (File.Exists(inputPath))
            {
                TryAddNode(CreateFileNode(inputPath, options), results, detector);
                return;
            }

            if (!Directory.Exists(inputPath))
            {
                return;
            }

            if (IsGeodatabasePath(inputPath))
            {
                TryAddNode(CreateGeodatabaseNode(inputPath, options), results, detector);
                return;
            }

            ScanDirectory(inputPath, options, results, detector);
        }

        private void ScanDirectory(string directoryPath, QuickDataImportOptions options, ICollection<QuickDataNode> results, QuickDataDuplicateDetector detector)
        {
            foreach (var filePath in SafeEnumerateFiles(directoryPath))
            {
                TryAddNode(CreateFileNode(filePath, options), results, detector);
            }

            foreach (var childDirectory in SafeEnumerateDirectories(directoryPath))
            {
                if (IsGeodatabasePath(childDirectory))
                {
                    TryAddNode(CreateGeodatabaseNode(childDirectory, options), results, detector);
                    continue;
                }

                if (options.Recurse)
                {
                    ScanDirectory(childDirectory, options, results, detector);
                }
            }
        }

        private QuickDataNode? CreateFileNode(string filePath, QuickDataImportOptions options)
        {
            var extension = Path.GetExtension(filePath);
            if (string.Equals(extension, ".shp", StringComparison.OrdinalIgnoreCase) && options.IncludeFeatureClasses)
            {
                var node = new QuickDataNode
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    NodeKind = QuickDataNodeKind.FeatureClass,
                    GeometryKind = GetGeometryKindFromShapefile(filePath),
                    ContainerPath = Path.GetDirectoryName(filePath) ?? string.Empty,
                    DatasetName = Path.GetFileName(filePath),
                    SourcePath = filePath,
                    CoordinateSystem = GetCoordinateSystemFromPrjSidecar(Path.ChangeExtension(filePath, ".prj"))
                };

                if (!options.Matches(node.Name))
                {
                    return null;
                }

                node.Normalize();
                return node;
            }

            if (RasterExtensions.Contains(extension) && options.IncludeRasters)
            {
                var node = new QuickDataNode
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    NodeKind = QuickDataNodeKind.Raster,
                    GeometryKind = QuickDataGeometryKind.None,
                    ContainerPath = Path.GetDirectoryName(filePath) ?? string.Empty,
                    DatasetName = Path.GetFileName(filePath),
                    SourcePath = filePath,
                    CoordinateSystem = GetCoordinateSystemFromRaster(filePath)
                };

                if (!options.Matches(node.Name))
                {
                    return null;
                }

                node.Normalize();
                return node;
            }

            if (LayerFileExtensions.Contains(extension) && options.IncludeLayerFiles)
            {
                var node = new QuickDataNode
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    NodeKind = QuickDataNodeKind.LayerFile,
                    GeometryKind = QuickDataGeometryKind.None,
                    ContainerPath = Path.GetDirectoryName(filePath) ?? string.Empty,
                    DatasetName = Path.GetFileName(filePath),
                    SourcePath = filePath,
                    CoordinateSystem = "图层文件"
                };

                if (!options.Matches(node.Name))
                {
                    return null;
                }

                node.Normalize();
                return node;
            }

            return null;
        }

        private QuickDataNode? CreateGeodatabaseNode(string geodatabasePath, QuickDataImportOptions options)
        {
            var geodatabaseNode = new QuickDataNode
            {
                Name = Path.GetFileNameWithoutExtension(geodatabasePath),
                NodeKind = QuickDataNodeKind.Geodatabase,
                GeometryKind = QuickDataGeometryKind.None,
                ContainerPath = Path.GetDirectoryName(geodatabasePath) ?? string.Empty,
                DatasetName = Path.GetFileName(geodatabasePath),
                SourcePath = geodatabasePath,
                CoordinateSystem = "文件地理数据库"
            };

            var datasetNodes = new Dictionary<string, QuickDataNode>(StringComparer.OrdinalIgnoreCase);
            var directChildren = new List<QuickDataNode>();

            try
            {
                foreach (var child in _geodatabaseInspector.Inspect(geodatabasePath))
                {
                    if (child.NodeKind == QuickDataNodeKind.FeatureClass && !options.IncludeFeatureClasses)
                    {
                        continue;
                    }

                    if (child.NodeKind == QuickDataNodeKind.Table && !options.IncludeTables)
                    {
                        continue;
                    }

                    if (child.NodeKind == QuickDataNodeKind.FeatureDataset)
                    {
                        var featureDatasetNode = new QuickDataNode
                        {
                            Name = child.Name,
                            NodeKind = QuickDataNodeKind.FeatureDataset,
                            GeometryKind = QuickDataGeometryKind.None,
                            ContainerPath = geodatabasePath,
                            DatasetName = string.IsNullOrWhiteSpace(child.DatasetPath) ? child.Name : child.DatasetPath,
                            SourcePath = Path.Combine(geodatabasePath, string.IsNullOrWhiteSpace(child.DatasetPath) ? child.Name : child.DatasetPath),
                            CoordinateSystem = string.IsNullOrWhiteSpace(child.CoordinateSystem) ? "未知" : child.CoordinateSystem
                        };

                        featureDatasetNode.Normalize();
                        datasetNodes[featureDatasetNode.DatasetName] = featureDatasetNode;

                        if (options.Matches(featureDatasetNode.Name))
                        {
                            directChildren.Add(featureDatasetNode);
                        }

                        continue;
                    }

                    var datasetPath = string.IsNullOrWhiteSpace(child.DatasetPath) ? child.Name : child.DatasetPath;
                    var childNode = new QuickDataNode
                    {
                        Name = child.Name,
                        NodeKind = child.NodeKind,
                        GeometryKind = child.GeometryKind,
                        ContainerPath = geodatabasePath,
                        DatasetName = datasetPath,
                        SourcePath = Path.Combine(geodatabasePath, datasetPath),
                        CoordinateSystem = string.IsNullOrWhiteSpace(child.CoordinateSystem) ? "未知" : child.CoordinateSystem
                    };

                    childNode.Normalize();

                    if (!options.Matches(childNode.Name))
                    {
                        if (child.NodeKind == QuickDataNodeKind.Table)
                        {
                            continue;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(child.ParentDatasetPath)
                        && datasetNodes.TryGetValue(child.ParentDatasetPath, out var parentDatasetNode))
                    {
                        parentDatasetNode.Children.Add(childNode);

                        if (!directChildren.Contains(parentDatasetNode))
                        {
                            directChildren.Add(parentDatasetNode);
                        }
                    }
                    else
                    {
                        directChildren.Add(childNode);
                    }
                }
            }
            catch
            {
                // 允许保留空数据库节点，后续可手动刷新。
            }

            foreach (var featureDatasetNode in datasetNodes.Values)
            {
                featureDatasetNode.Children = featureDatasetNode.Children
                    .GroupBy(node => node.UniqueKey, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(node => node.Name)
                    .ToList();
            }

            geodatabaseNode.Children = directChildren
                .Where(node => node.NodeKind != QuickDataNodeKind.FeatureDataset || node.Children.Count > 0 || options.Matches(node.Name))
                .GroupBy(node => node.UniqueKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(node => node.Name)
                .ToList();

            if (!options.Matches(geodatabaseNode.Name) && geodatabaseNode.Children.Count == 0)
            {
                return null;
            }

            geodatabaseNode.Normalize();
            return geodatabaseNode;
        }

        private static void TryAddNode(QuickDataNode? node, ICollection<QuickDataNode> results, QuickDataDuplicateDetector detector)
        {
            if (node == null)
            {
                return;
            }

            if (!detector.TryRegister(node))
            {
                return;
            }

            results.Add(node);
        }

        private static IEnumerable<string> SafeEnumerateFiles(string directoryPath)
        {
            try
            {
                return Directory.EnumerateFiles(directoryPath);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static IEnumerable<string> SafeEnumerateDirectories(string directoryPath)
        {
            try
            {
                return Directory.EnumerateDirectories(directoryPath);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static bool IsGeodatabasePath(string path)
        {
            return path.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase);
        }

        private static QuickDataGeometryKind GetGeometryKindFromShapefile(string shapefilePath)
        {
            try
            {
                using var stream = File.OpenRead(shapefilePath);
                if (stream.Length < 36)
                {
                    return QuickDataGeometryKind.Unknown;
                }

                var header = new byte[36];
                stream.Read(header, 0, header.Length);
                var shapeType = BitConverter.ToInt32(header, 32);
                return shapeType switch
                {
                    1 or 11 or 21 => QuickDataGeometryKind.Point,
                    3 or 13 or 23 => QuickDataGeometryKind.Polyline,
                    5 or 15 or 25 => QuickDataGeometryKind.Polygon,
                    8 or 18 or 28 => QuickDataGeometryKind.Multipoint,
                    _ => QuickDataGeometryKind.Unknown
                };
            }
            catch
            {
                return QuickDataGeometryKind.Unknown;
            }
        }

        private static string GetCoordinateSystemFromRaster(string rasterPath)
        {
            var prjPath = Path.ChangeExtension(rasterPath, ".prj");
            var coordinateSystem = GetCoordinateSystemFromPrjSidecar(prjPath);
            if (!string.Equals(coordinateSystem, "未知", StringComparison.Ordinal))
            {
                return coordinateSystem;
            }

            return Path.GetExtension(rasterPath).Equals(".tif", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(rasterPath).Equals(".tiff", StringComparison.OrdinalIgnoreCase)
                ? "GeoTIFF"
                : "未知";
        }

        private static string GetCoordinateSystemFromPrjSidecar(string prjPath)
        {
            try
            {
                if (!File.Exists(prjPath))
                {
                    return "未知";
                }

                return ParseCoordinateSystem(File.ReadAllText(prjPath));
            }
            catch
            {
                return "未知";
            }
        }

        private static string ParseCoordinateSystem(string prjContent)
        {
            if (string.IsNullOrWhiteSpace(prjContent))
            {
                return "未知";
            }

            var trimmed = prjContent.Trim();
            var quoteStart = trimmed.IndexOf('"');
            if ((trimmed.StartsWith("GEOGCS[", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("PROJCS[", StringComparison.OrdinalIgnoreCase))
                && quoteStart >= 0)
            {
                var quoteEnd = trimmed.IndexOf('"', quoteStart + 1);
                if (quoteEnd > quoteStart)
                {
                    return NormalizeCoordinateSystemName(trimmed.Substring(quoteStart + 1, quoteEnd - quoteStart - 1));
                }
            }

            if (trimmed.Contains("WGS_1984", StringComparison.OrdinalIgnoreCase))
            {
                return "WGS 1984";
            }

            if (trimmed.Contains("Beijing_1954", StringComparison.OrdinalIgnoreCase))
            {
                return "Beijing 1954";
            }

            if (trimmed.Contains("Xian_1980", StringComparison.OrdinalIgnoreCase))
            {
                return "Xian 1980";
            }

            if (trimmed.Contains("CGCS2000", StringComparison.OrdinalIgnoreCase))
            {
                return "CGCS2000";
            }

            return "未知";
        }

        private static string NormalizeCoordinateSystemName(string coordinateSystemName)
        {
            if (string.IsNullOrWhiteSpace(coordinateSystemName))
            {
                return "未知";
            }

            if (coordinateSystemName.Contains("WGS_1984", StringComparison.OrdinalIgnoreCase))
            {
                return "WGS 1984";
            }

            if (coordinateSystemName.Contains("Beijing_1954", StringComparison.OrdinalIgnoreCase))
            {
                return "Beijing 1954";
            }

            if (coordinateSystemName.Contains("Xian_1980", StringComparison.OrdinalIgnoreCase))
            {
                return "Xian 1980";
            }

            if (coordinateSystemName.Contains("CGCS2000", StringComparison.OrdinalIgnoreCase))
            {
                return coordinateSystemName;
            }

            return coordinateSystemName;
        }

        private sealed class NullQuickDataGeodatabaseInspector : IQuickDataGeodatabaseInspector
        {
            public IReadOnlyList<QuickDataGeodatabaseChild> Inspect(string geodatabasePath)
            {
                return Array.Empty<QuickDataGeodatabaseChild>();
            }
        }
    }
}
