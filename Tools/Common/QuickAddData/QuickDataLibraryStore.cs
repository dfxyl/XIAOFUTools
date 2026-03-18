#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XIAOFUTools.Tools.QuickAddData
{
    public sealed class QuickDataLibraryStore
    {
        public const int CurrentVersion = 1;

        private readonly string _filePath;

        public QuickDataLibraryStore(string? filePath = null)
        {
            _filePath = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "XIAOFUTools",
                    "QuickDataLibrary.json")
                : filePath;
        }

        public QuickDataLibraryDocument Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return CreateEmptyDocument();
                }

                var json = File.ReadAllText(_filePath);
                var document = JsonSerializer.Deserialize<QuickDataLibraryDocument>(json);
                return NormalizeDocument(document);
            }
            catch
            {
                return CreateEmptyDocument();
            }
        }

        public void Save(QuickDataLibraryDocument document)
        {
            var normalized = NormalizeDocument(document);
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_filePath, json);
        }

        private static QuickDataLibraryDocument NormalizeDocument(QuickDataLibraryDocument? document)
        {
            document ??= CreateEmptyDocument();
            document.Version = document.Version <= 0 ? CurrentVersion : document.Version;
            document.Groups ??= new System.Collections.Generic.List<QuickDataGroup>();
            document.TreeViewState ??= new QuickDataTreeViewState();
            document.TreeViewState.ExpandedKeys ??= new List<string>();
            document.TreeViewState.ExpandedKeys = document.TreeViewState.ExpandedKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var group in document.Groups)
            {
                group.Id = string.IsNullOrWhiteSpace(group.Id) ? Guid.NewGuid().ToString("N") : group.Id;
                group.Name ??= string.Empty;
                group.Nodes ??= new System.Collections.Generic.List<QuickDataNode>();

                foreach (var node in group.Nodes)
                {
                    NormalizeNode(node);
                }
            }

            return document;
        }

        private static void NormalizeNode(QuickDataNode node)
        {
            if (node == null)
            {
                return;
            }

            node.Id = string.IsNullOrWhiteSpace(node.Id) ? Guid.NewGuid().ToString("N") : node.Id;
            node.Name ??= string.Empty;
            node.ContainerPath ??= string.Empty;
            node.DatasetName ??= string.Empty;
            node.SourcePath ??= string.Empty;
            node.CoordinateSystem = string.IsNullOrWhiteSpace(node.CoordinateSystem) ? "未知" : node.CoordinateSystem;
            node.Children ??= new System.Collections.Generic.List<QuickDataNode>();
            node.Normalize();

            foreach (var child in node.Children)
            {
                NormalizeNode(child);
            }
        }

        private static QuickDataLibraryDocument CreateEmptyDocument()
        {
            return new QuickDataLibraryDocument
            {
                Version = CurrentVersion,
                TreeViewState = new QuickDataTreeViewState()
            };
        }
    }
}
