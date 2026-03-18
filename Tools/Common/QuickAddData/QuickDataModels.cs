using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Tools.QuickAddData
{
    public enum QuickDataNodeKind
    {
        FeatureClass,
        Table,
        Raster,
        Geodatabase,
        LayerFile,
        FeatureDataset
    }

    public enum QuickDataGeometryKind
    {
        None,
        Point,
        Polyline,
        Polygon,
        Multipoint,
        Unknown
    }

    public sealed class QuickDataLibraryDocument
    {
        public int Version { get; set; } = QuickDataLibraryStore.CurrentVersion;

        public List<QuickDataGroup> Groups { get; set; } = new List<QuickDataGroup>();

        public QuickDataTreeViewState TreeViewState { get; set; } = new QuickDataTreeViewState();
    }

    public sealed class QuickDataTreeViewState
    {
        public List<string> ExpandedKeys { get; set; } = new List<string>();
    }

    public sealed class QuickDataGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;

        public List<QuickDataNode> Nodes { get; set; } = new List<QuickDataNode>();
    }

    public sealed class QuickDataNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;

        public QuickDataNodeKind NodeKind { get; set; }

        public QuickDataGeometryKind GeometryKind { get; set; } = QuickDataGeometryKind.None;

        public string ContainerPath { get; set; } = string.Empty;

        public string DatasetName { get; set; } = string.Empty;

        public string SourcePath { get; set; } = string.Empty;

        public string CoordinateSystem { get; set; } = "未知";

        public string UniqueKey { get; set; } = string.Empty;

        public List<QuickDataNode> Children { get; set; } = new List<QuickDataNode>();

        public bool IsLoadableLeaf => NodeKind != QuickDataNodeKind.Geodatabase && NodeKind != QuickDataNodeKind.FeatureDataset;

        public void Normalize()
        {
            UniqueKey = BuildUniqueKey(ContainerPath, DatasetName, NodeKind);

            foreach (var child in Children)
            {
                child.Normalize();
            }
        }

        public QuickDataNode CloneDeep()
        {
            var clone = new QuickDataNode
            {
                Id = Id,
                Name = Name,
                NodeKind = NodeKind,
                GeometryKind = GeometryKind,
                ContainerPath = ContainerPath,
                DatasetName = DatasetName,
                SourcePath = SourcePath,
                CoordinateSystem = CoordinateSystem,
                UniqueKey = UniqueKey
            };

            clone.Children.AddRange(Children.Select(child => child.CloneDeep()));
            return clone;
        }

        public static string BuildUniqueKey(string containerPath, string datasetName, QuickDataNodeKind nodeKind)
        {
            var normalizedContainer = NormalizePath(containerPath);
            var normalizedDataset = (datasetName ?? string.Empty).Trim().Replace('/', '\\').ToUpperInvariant();
            return $"{normalizedContainer}|{normalizedDataset}|{nodeKind}";
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                var normalized = Path.GetFullPath(path);
                normalized = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return normalized.Replace('/', '\\').ToUpperInvariant();
            }
            catch
            {
                return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('/', '\\')
                    .ToUpperInvariant();
            }
        }
    }

    public sealed class QuickDataImportOptions
    {
        public bool IncludeFeatureClasses { get; set; } = true;

        public bool IncludeTables { get; set; } = true;

        public bool IncludeRasters { get; set; } = true;

        public bool IncludeLayerFiles { get; set; } = true;

        public bool Recurse { get; set; } = true;

        public string Keyword { get; set; } = string.Empty;

        public bool Matches(string text)
        {
            if (string.IsNullOrWhiteSpace(Keyword))
            {
                return true;
            }

            return text?.IndexOf(Keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public sealed class QuickDataGeodatabaseChild
    {
        public string Name { get; set; } = string.Empty;

        public string DatasetPath { get; set; } = string.Empty;

        public string ParentDatasetPath { get; set; } = string.Empty;

        public QuickDataNodeKind NodeKind { get; set; }

        public QuickDataGeometryKind GeometryKind { get; set; } = QuickDataGeometryKind.None;

        public string CoordinateSystem { get; set; } = "未知";
    }

    public interface IQuickDataGeodatabaseInspector
    {
        IReadOnlyList<QuickDataGeodatabaseChild> Inspect(string geodatabasePath);
    }

    public sealed class QuickDataDuplicateDetector
    {
        private readonly HashSet<string> _knownKeys;

        public QuickDataDuplicateDetector(IEnumerable<string> existingKeys)
        {
            _knownKeys = new HashSet<string>(
                existingKeys?
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Select(key => key.Trim())
                    ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        public int SkippedCount { get; private set; }

        public bool TryRegister(QuickDataNode node)
        {
            if (node == null)
            {
                return false;
            }

            var uniqueKey = string.IsNullOrWhiteSpace(node.UniqueKey)
                ? QuickDataNode.BuildUniqueKey(node.ContainerPath, node.DatasetName, node.NodeKind)
                : node.UniqueKey;

            if (_knownKeys.Contains(uniqueKey))
            {
                SkippedCount++;
                return false;
            }

            _knownKeys.Add(uniqueKey);
            return true;
        }
    }
}
