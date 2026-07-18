#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace XIAOFUTools.Features.Custom.GisToolPocket.Core
{
    public sealed class ToolboxConfiguration
    {
        public string ToolboxPath { get; set; } = string.Empty;

        public ToolboxCatalog Catalog { get; set; } = new();

        public bool IsPackage { get; set; }

        public List<ToolboxCatalog> Catalogs { get; set; } = new();

        public string MenuMode { get; set; } = "Auto";
    }

    public sealed class ToolboxCatalog : INotifyPropertyChanged
    {
        private string _displayName = string.Empty;
        private string _menuMode = "Inherit";

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ToolboxPath { get; set; } = string.Empty;

        public string OriginalToolboxPath { get; set; } = string.Empty;

        public string CatalogKind { get; set; } = "Toolbox";

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName == value)
                    return;

                _displayName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }

        public string Alias { get; set; } = string.Empty;

        public bool IsVisible { get; set; } = true;

        public string MenuMode
        {
            get => _menuMode;
            set
            {
                if (_menuMode == value)
                    return;

                _menuMode = value;
                OnPropertyChanged();
            }
        }

        public List<ToolboxToolset> Toolsets { get; set; } = new();

        public ToolboxPackageManifest? PackageManifest { get; set; }

        [JsonIgnore]
        public int ToolCount => Toolsets.Sum(CountTools);

        [JsonIgnore]
        public int ToolsetCount => Toolsets.Sum(CountToolsets);

        [JsonIgnore]
        public string SourceFileName => ToolboxCatalogRules.IsCustomCatalog(this)
            ? "自定义组"
            : string.IsNullOrWhiteSpace(ToolboxPath)
                ? string.Empty
                : System.IO.Path.GetFileName(ToolboxPath);

        [JsonIgnore]
        public string DisplayTitle => string.IsNullOrWhiteSpace(DisplayName)
            ? SourceFileName
            : DisplayName;

        [JsonIgnore]
        public string Summary => $"{ToolsetCount} 个工具集，{ToolCount} 个工具";

        private static int CountTools(ToolboxToolset toolset)
        {
            return toolset.Tools.Count + toolset.Children.Sum(CountTools);
        }

        private static int CountToolsets(ToolboxToolset toolset)
        {
            var self = ToolboxCatalogRules.IsDefaultToolsetName(toolset.Name) ? 0 : 1;
            return self + toolset.Children.Sum(CountToolsets);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class ToolboxToolset
    {
        public string Name { get; set; } = string.Empty;

        public bool IsVisible { get; set; } = true;

        public List<ToolboxToolset> Children { get; set; } = new();

        public List<ToolboxTool> Tools { get; set; } = new();

        [JsonIgnore]
        public int ToolCount => Tools.Count + Children.Sum(child => child.ToolCount);
    }

    public sealed class ToolboxTool
    {
        public string Name { get; set; } = string.Empty;

        public string Caption { get; set; } = string.Empty;

        public string ToolPath { get; set; } = string.Empty;

        public string SourceToolPath { get; set; } = string.Empty;

        public string ToolKind { get; set; } = "Toolbox";

        public bool IsVisible { get; set; } = true;
    }

    public sealed class ToolboxPackageManifest
    {
        public string FormatVersion { get; set; } = "1";

        public string PackageKind { get; set; } = "AddInToolboxPackage";

        public string PackageName { get; set; } = string.Empty;

        public string CreatedAt { get; set; } = string.Empty;

        public string MenuMode { get; set; } = "Auto";

        public List<ToolboxPackageEntry> Toolboxes { get; set; } = new();
    }

    public sealed class ToolboxPackageEntry
    {
        public string RelativePath { get; set; } = string.Empty;

        public string OriginalPath { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string CatalogKind { get; set; } = "Toolbox";

        public string MenuMode { get; set; } = "Inherit";

        public string Alias { get; set; } = string.Empty;

        public bool IsVisible { get; set; } = true;

        public List<ToolboxToolset> Toolsets { get; set; } = new();
    }
}
