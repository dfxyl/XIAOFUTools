#nullable enable

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Core.Geoprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using XIAOFUTools.Features.Custom.GisToolPocket.Application;
using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Presentation
{
    internal enum ToolboxMenuSlotKind
    {
        Toolbox,
        Toolset,
        Flat
    }

    internal sealed class ToolboxMenuSlot
    {
        public int Index { get; init; }

        public string Caption { get; init; } = string.Empty;

        public string Tooltip { get; init; } = string.Empty;

        public string IconBaseName { get; init; } = "Toolbox";

        public ToolboxMenuSlotKind Kind { get; init; }

        public ToolboxCatalog Catalog { get; init; } = new();

        public ToolboxToolset? Toolset { get; init; }
    }

    internal static class ToolboxMenuSlotService
    {
        public const int MaxSlots = 32;

        private const string SlotStatePrefix = "GIS_Toolbox_ToolSlot";
        private const string SlotStateSuffix = "_State";
        private const string SlotMenuPrefix = "GIS_Toolbox_ToolSlot";
        private const string SlotMenuSuffix = "Menu";
        private const string SlotGroupSuffix = "Group";

        public static List<ToolboxMenuSlot> GetSlots()
        {
            var configuration = ToolboxApplicationService.Load();
            var catalogs = LoadUsableCatalogs(configuration);
            var slots = BuildSlots(catalogs, configuration.MenuMode);

            return slots.Take(MaxSlots).ToList();
        }

        public static string FormatRibbonCaption(string caption)
        {
            if (string.IsNullOrWhiteSpace(caption) || caption.Length <= 8)
                return caption;

            var separators = new[] { '/', '\\', '-', '_', ' ', '·' };
            foreach (var separator in separators)
            {
                var index = caption.IndexOf(separator);
                if (index > 1 && index < caption.Length - 1)
                    return caption[..index] + Environment.NewLine + caption[(index + 1)..];
            }

            var splitIndex = Math.Min(8, Math.Max(4, caption.Length / 2));
            return caption[..splitIndex] + Environment.NewLine + caption[splitIndex..];
        }

        public static void RefreshRibbon()
        {
            var slots = GetSlots().ToDictionary(slot => slot.Index);

            for (var i = 1; i <= MaxSlots; i++)
            {
                var stateId = BuildSlotStateId(i);
                var menuId = BuildSlotMenuId(i);
                var hasSlot = slots.TryGetValue(i, out var slot);

                if (hasSlot)
                    FrameworkApplication.State.Activate(stateId);
                else
                    FrameworkApplication.State.Deactivate(stateId);

                var groupWrapper = FrameworkApplication.GetPlugInWrapper(BuildSlotGroupId(i));
                if (groupWrapper is not null)
                    groupWrapper.Caption = " ";

                var menuWrapper = FrameworkApplication.GetPlugInWrapper(menuId);
                if (menuWrapper is null)
                    continue;

                menuWrapper.Caption = hasSlot ? FormatRibbonCaption(slot!.Caption) : $"槽位{i:00}";
                menuWrapper.Tooltip = hasSlot ? slot!.Tooltip : "未绑定工具箱";
                if (hasSlot)
                {
                    menuWrapper.SmallImage = ToolboxIconService.SmallImageSource(slot!.IconBaseName);
                    menuWrapper.LargeImage = ToolboxIconService.LargeImageSource(slot.IconBaseName);
                }
            }
        }

        public static ToolboxMenuSlot? GetSlotByControlId(string controlId)
        {
            var index = ParseSlotIndex(controlId);
            if (index <= 0)
                return null;

            return GetSlots().FirstOrDefault(slot => slot.Index == index);
        }

        private static List<ToolboxCatalog> LoadUsableCatalogs(ToolboxConfiguration configuration)
        {
            var catalogs = configuration.Catalogs
                .Where(catalog => ToolboxApplicationService.IsCustomCatalog(catalog) || !string.IsNullOrWhiteSpace(catalog.ToolboxPath))
                .ToList();

            var usableCatalogs = new List<ToolboxCatalog>();
            var changed = false;

            foreach (var catalog in catalogs)
            {
                if (!catalog.IsVisible)
                    continue;

                if (!ToolboxApplicationService.IsCustomCatalog(catalog) && !File.Exists(catalog.ToolboxPath))
                    continue;

                if (HasVisibleTools(catalog.Toolsets))
                {
                    usableCatalogs.Add(catalog);
                    continue;
                }
            }

            if (changed)
                ToolboxApplicationService.Save(usableCatalogs, configuration.MenuMode);

            return usableCatalogs;
        }

        private static List<ToolboxMenuSlot> BuildSlots(IReadOnlyCollection<ToolboxCatalog> catalogs, string globalMenuMode)
        {
            var slots = new List<ToolboxMenuSlot>();
            foreach (var catalog in catalogs.Where(catalog => catalog.IsVisible && HasVisibleTools(catalog.Toolsets)))
            {
                var mode = ResolveCatalogMenuMode(catalog, globalMenuMode, catalogs.Count);
                switch (mode)
                {
                    case "Flat":
                        slots.Add(CreateCatalogSlot(catalog, slots.Count + 1, ToolboxMenuSlotKind.Flat));
                        break;
                    case "Toolset":
                        slots.AddRange(BuildToolsetSlots(catalog, slots.Count + 1, catalogs.Count > 1));
                        break;
                    case "Toolbox":
                    case "Tree":
                    default:
                        slots.Add(CreateCatalogSlot(catalog, slots.Count + 1, ToolboxMenuSlotKind.Toolbox));
                        break;
                }
            }

            return slots;
        }

        private static string ResolveCatalogMenuMode(ToolboxCatalog catalog, string globalMenuMode, int catalogCount)
        {
            var mode = catalog.MenuMode;
            if (string.IsNullOrWhiteSpace(mode) || mode.Equals("Inherit", StringComparison.OrdinalIgnoreCase))
                mode = globalMenuMode;

            return mode switch
            {
                "Toolbox" => "Toolbox",
                "Toolset" => "Toolset",
                "Tree" => "Tree",
                "Flat" => "Flat",
                _ => "Toolbox"
            };
        }

        private static ToolboxMenuSlot CreateCatalogSlot(ToolboxCatalog catalog, int index, ToolboxMenuSlotKind kind)
        {
            return new ToolboxMenuSlot
            {
                Index = index,
                Caption = catalog.DisplayTitle,
                Tooltip = catalog.Summary,
                IconBaseName = ToolboxIconService.SlotIconBaseName(index),
                Kind = kind,
                Catalog = catalog
            };
        }

        private static List<ToolboxMenuSlot> BuildToolboxSlots(IReadOnlyCollection<ToolboxCatalog> catalogs)
        {
            var index = 1;
            return catalogs
                .Where(catalog => catalog.IsVisible && HasVisibleTools(catalog.Toolsets))
                .Select(catalog => new ToolboxMenuSlot
                {
                    Index = index,
                    Caption = catalog.DisplayTitle,
                    Tooltip = catalog.Summary,
                    IconBaseName = ToolboxIconService.SlotIconBaseName(index++),
                    Kind = ToolboxMenuSlotKind.Toolbox,
                    Catalog = catalog
                })
                .ToList();
        }

        private static List<ToolboxMenuSlot> BuildToolsetSlots(ToolboxCatalog catalog, int startIndex, bool prefixCatalogTitle)
        {
            var index = startIndex;
            var slots = new List<ToolboxMenuSlot>();

            foreach (var toolset in VisibleToolsets(catalog.Toolsets))
            {
                var slotIndex = index++;
                var toolsetTitle = string.IsNullOrWhiteSpace(toolset.Name) ? "工具集" : toolset.Name;
                var caption = IsDefaultToolset(toolset)
                    ? catalog.DisplayTitle
                    : prefixCatalogTitle ? $"{catalog.DisplayTitle}/{toolsetTitle}" : toolsetTitle;

                slots.Add(new ToolboxMenuSlot
                {
                    Index = slotIndex,
                    Caption = caption,
                    Tooltip = $"{catalog.DisplayTitle} · {CountVisibleTools(toolset)} 个工具",
                    IconBaseName = ToolboxIconService.SlotIconBaseName(slotIndex),
                    Kind = ToolboxMenuSlotKind.Toolset,
                    Catalog = catalog,
                    Toolset = toolset
                });
            }

            return slots;
        }

        private static IEnumerable<ToolboxToolset> VisibleToolsets(IEnumerable<ToolboxToolset> toolsets)
        {
            return toolsets.Where(toolset => toolset.IsVisible && HasVisibleTools(toolset));
        }

        public static bool HasVisibleTools(IEnumerable<ToolboxToolset> toolsets)
        {
            return toolsets.Any(HasVisibleTools);
        }

        public static bool HasVisibleTools(ToolboxToolset toolset)
        {
            if (!toolset.IsVisible)
                return false;

            return toolset.Tools.Any(tool => tool.IsVisible && !string.IsNullOrWhiteSpace(tool.ToolPath)) ||
                   toolset.Children.Any(HasVisibleTools);
        }

        public static int CountVisibleTools(ToolboxToolset toolset)
        {
            if (!toolset.IsVisible)
                return 0;

            return toolset.Tools.Count(tool => tool.IsVisible && !string.IsNullOrWhiteSpace(tool.ToolPath)) +
                   toolset.Children.Sum(CountVisibleTools);
        }

        public static bool IsDefaultToolset(ToolboxToolset toolset)
        {
            return ToolboxApplicationService.IsDefaultToolsetName(toolset.Name);
        }

        private static string BuildSlotStateId(int index)
        {
            return $"{SlotStatePrefix}{index:00}{SlotStateSuffix}";
        }

        private static string BuildSlotMenuId(int index)
        {
            return $"{SlotMenuPrefix}{index:00}{SlotMenuSuffix}";
        }

        private static string BuildSlotGroupId(int index)
        {
            return $"{SlotMenuPrefix}{index:00}{SlotGroupSuffix}";
        }

        private static int ParseSlotIndex(string controlId)
        {
            var match = Regex.Match(controlId, @"ToolSlot(?<index>\d+)Menu$", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups["index"].Value, out var index) ? index : -1;
        }

        public static async Task OpenToolAsync(ToolboxTool tool)
        {
            if (tool is null || string.IsNullOrWhiteSpace(tool.ToolPath))
            {
                MessageBox.Show("工具路径为空，无法打开。", "GIS 工具口袋");
                return;
            }

            if (ToolboxApplicationService.IsCommandTool(tool))
            {
                var action = FrameworkApplication.ExecuteCommand(tool.ToolPath);
                if (action is null)
                {
                    MessageBox.Show($"命令当前不可执行或不存在：{tool.ToolPath}", "GIS 工具口袋");
                    return;
                }

                await action();
                return;
            }

            if (ToolboxApplicationService.IsMapTool(tool))
            {
                await FrameworkApplication.SetCurrentToolAsync(tool.ToolPath);
                return;
            }

            var parameters = Geoprocessing.MakeValueArray();
            await Geoprocessing.OpenToolDialogAsync(tool.ToolPath, parameters);
        }
    }

    internal sealed class ToolboxSlotDynamicMenu : DynamicMenu
    {
        private static readonly string ToolsetIcon16 = ToolboxIconService.SmallImageUri(ToolboxIconService.ToolsetIconBaseName);
        private static readonly string ToolIcon16 = ToolboxIconService.SmallImageUri(ToolboxIconService.ToolIconBaseName);

        private delegate void OpenToolHandler(ToolboxTool tool);

        protected override void OnUpdate()
        {
            var slot = ToolboxMenuSlotService.GetSlotByControlId(ID);
            if (slot is null)
            {
                Enabled = false;
                return;
            }

            Caption = ToolboxMenuSlotService.FormatRibbonCaption(slot.Caption);
            TooltipHeading = slot.Caption;
            Tooltip = slot.Tooltip;
            SmallImage = ToolboxIconService.SmallImageSource(slot.IconBaseName);
            LargeImage = ToolboxIconService.LargeImageSource(slot.IconBaseName);
            Enabled = true;
        }

        protected override void OnPopup()
        {
            var slot = ToolboxMenuSlotService.GetSlotByControlId(ID);
            if (slot is null)
            {
                Add("未绑定工具箱", ToolsetIcon16, false, false, false);
                return;
            }

            if (slot.Kind == ToolboxMenuSlotKind.Flat)
            {
                AddFlatTools(slot.Catalog);
                return;
            }

            if (slot.Kind == ToolboxMenuSlotKind.Toolset && slot.Toolset is not null)
            {
                AddToolsetContents(slot.Catalog, slot.Toolset);
                return;
            }

            AddToolsetsOrTools(slot.Catalog);
        }

        private void AddToolsetsOrTools(ToolboxCatalog catalog)
        {
            var toolsets = catalog.Toolsets
                .Where(toolset => toolset.IsVisible && ToolboxMenuSlotService.HasVisibleTools(toolset))
                .ToList();

            if (toolsets.Count == 0)
            {
                Add("没有可用工具", ToolIcon16, false, false, false);
                return;
            }

            var defaultToolsets = toolsets
                .Where(ToolboxMenuSlotService.IsDefaultToolset)
                .ToList();
            var namedToolsets = toolsets
                .Where(toolset => !ToolboxMenuSlotService.IsDefaultToolset(toolset))
                .ToList();

            if (defaultToolsets.Count > 0)
            {
                foreach (var defaultToolset in defaultToolsets)
                    AddToolsetContents(catalog, defaultToolset);

                if (namedToolsets.Count > 0)
                    AddSeparator();
            }

            foreach (var toolset in namedToolsets)
            {
                Add(new ToolboxToolsetDynamicMenu(catalog, toolset), () => toolset.Name);
            }
        }

        private void AddToolsetContents(ToolboxCatalog catalog, ToolboxToolset toolset)
        {
            var visibleChildren = toolset.Children
                .Where(child => child.IsVisible && ToolboxMenuSlotService.HasVisibleTools(child))
                .ToList();

            foreach (var child in visibleChildren)
                Add(new ToolboxToolsetDynamicMenu(catalog, child), () => child.Name);

            if (visibleChildren.Count > 0 && toolset.Tools.Any(tool => tool.IsVisible && !string.IsNullOrWhiteSpace(tool.ToolPath)))
                AddSeparator();

            AddTools(toolset);
        }

        private void AddFlatTools(ToolboxCatalog catalog)
        {
            var tools = EnumerateTools(catalog.Toolsets).ToList();
            if (tools.Count == 0)
            {
                Add("没有可用工具", ToolIcon16, false, false, false);
                return;
            }

            var openTool = new OpenToolHandler(OpenToolEntry);
            for (var i = 0; i < tools.Count; i++)
            {
                var tool = tools[i];
                var caption = string.IsNullOrWhiteSpace(tool.Caption) ? tool.Name : tool.Caption;
                Add(ToolboxMenuSlotService.FormatRibbonCaption(caption), ToolIcon16, false, true, false, openTool, tool);
            }
        }

        private static IEnumerable<ToolboxTool> EnumerateTools(IEnumerable<ToolboxToolset> toolsets)
        {
            foreach (var toolset in toolsets)
            {
                if (!toolset.IsVisible)
                    continue;

                foreach (var tool in toolset.Tools)
                {
                    if (tool.IsVisible)
                        yield return tool;
                }

                foreach (var tool in EnumerateTools(toolset.Children))
                    yield return tool;
            }
        }

        private void AddTools(ToolboxToolset toolset)
        {
            var openTool = new OpenToolHandler(OpenToolEntry);
            var tools = toolset.Tools
                .Where(tool => tool.IsVisible && !string.IsNullOrWhiteSpace(tool.ToolPath))
                .ToList();

            if (tools.Count == 0)
            {
                Add("没有可用工具", ToolIcon16, false, false, false);
                return;
            }

            for (var i = 0; i < tools.Count; i++)
            {
                var tool = tools[i];
                var caption = string.IsNullOrWhiteSpace(tool.Caption) ? tool.Name : tool.Caption;
                Add(ToolboxMenuSlotService.FormatRibbonCaption(caption), ToolIcon16, false, true, false, openTool, tool);
            }
        }

        private static async void OpenToolEntry(ToolboxTool tool)
        {
            try
            {
                await ToolboxMenuSlotService.OpenToolAsync(tool);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开工具失败：{ex.Message}", "GIS 工具口袋");
            }
        }
    }

    internal sealed class ToolboxToolsetDynamicMenu : DynamicMenu
    {
        private static readonly string ToolIcon16 = ToolboxIconService.SmallImageUri(ToolboxIconService.ToolIconBaseName);

        private readonly ToolboxCatalog _catalog;
        private readonly ToolboxToolset _toolset;

        private delegate void OpenToolHandler(ToolboxTool tool);

        public ToolboxToolsetDynamicMenu(ToolboxCatalog catalog, ToolboxToolset toolset)
        {
            _catalog = catalog;
            _toolset = toolset;
            var caption = string.IsNullOrWhiteSpace(toolset.Name) ? "工具集" : toolset.Name;
            Caption = ToolboxMenuSlotService.FormatRibbonCaption(caption);
            TooltipHeading = caption;
            Tooltip = $"{catalog.DisplayTitle} · {ToolboxMenuSlotService.CountVisibleTools(toolset)} 个工具";
            var iconBaseName = ToolboxIconService.ToolsetIconBaseName;
            SmallImage = ToolboxIconService.SmallImageSource(iconBaseName);
            LargeImage = ToolboxIconService.LargeImageSource(iconBaseName);
        }

        protected override void OnPopup()
        {
            var openTool = new OpenToolHandler(OpenToolEntry);
            var visibleChildren = _toolset.Children
                .Where(child => child.IsVisible && ToolboxMenuSlotService.CountVisibleTools(child) > 0)
                .ToList();

            foreach (var child in visibleChildren)
                Add(new ToolboxToolsetDynamicMenu(_catalog, child), () => child.Name);

            if (visibleChildren.Count > 0 && _toolset.Tools.Any(tool => tool.IsVisible && !string.IsNullOrWhiteSpace(tool.ToolPath)))
                AddSeparator();

            var tools = _toolset.Tools
                .Where(tool => tool.IsVisible && !string.IsNullOrWhiteSpace(tool.ToolPath))
                .ToList();

            for (var i = 0; i < tools.Count; i++)
            {
                var tool = tools[i];
                var caption = string.IsNullOrWhiteSpace(tool.Caption) ? tool.Name : tool.Caption;
                Add(ToolboxMenuSlotService.FormatRibbonCaption(caption), ToolIcon16, false, true, false, openTool, tool);
            }
        }

        private static async void OpenToolEntry(ToolboxTool tool)
        {
            try
            {
                await ToolboxMenuSlotService.OpenToolAsync(tool);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开工具失败：{ex.Message}", "GIS 工具口袋");
            }
        }
    }
}
