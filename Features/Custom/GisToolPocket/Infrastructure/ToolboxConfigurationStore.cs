using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Infrastructure
{
    internal static class ToolboxConfigurationStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        public static string ConfigurationFilePath
        {
            get
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(root, "XIAOFUTools", "GisToolPocket", "toolbox-loader.json");
            }
        }

        private static string LegacyConfigurationFilePath
        {
            get
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(root, "GIS Toolbox", "toolbox-loader.json");
            }
        }

        public static ToolboxConfiguration Load()
        {
            try
            {
                var configurationPath = File.Exists(ConfigurationFilePath)
                    ? ConfigurationFilePath
                    : LegacyConfigurationFilePath;

                if (!File.Exists(configurationPath))
                    return new ToolboxConfiguration();

                var json = File.ReadAllText(configurationPath);
                var configuration = JsonSerializer.Deserialize<ToolboxConfiguration>(json, SerializerOptions) ?? new ToolboxConfiguration();
                Normalize(configuration);
                return configuration;
            }
            catch
            {
                return new ToolboxConfiguration();
            }
        }

        public static void Save(ToolboxCatalog catalog)
        {
            Save(new[] { catalog });
        }

        public static void SavePackage(IEnumerable<ToolboxCatalog> catalogs, string menuMode)
        {
            SavePackageState(catalogs, menuMode, true);
        }

        public static void Save(IEnumerable<ToolboxCatalog> catalogs)
        {
            Save(catalogs, "Auto");
        }

        public static void Save(IEnumerable<ToolboxCatalog> catalogs, string menuMode)
        {
            var normalizedCatalogs = NormalizeCatalogs(catalogs);
            var configuration = new ToolboxConfiguration
            {
                Catalogs = normalizedCatalogs,
                MenuMode = NormalizeMenuMode(menuMode)
            };

            if (configuration.Catalogs.Count > 0)
            {
                configuration.Catalog = configuration.Catalogs[0];
                configuration.ToolboxPath = configuration.Catalogs[0].ToolboxPath;
            }
            else
            {
                configuration.Catalog = new ToolboxCatalog();
                configuration.ToolboxPath = string.Empty;
            }

            var directory = Path.GetDirectoryName(ConfigurationFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(configuration, SerializerOptions);
            File.WriteAllText(ConfigurationFilePath, json);
        }

        public static void SavePackageState(IEnumerable<ToolboxCatalog> catalogs, string menuMode, bool isPackage = true)
        {
            var normalizedCatalogs = NormalizeCatalogs(catalogs);
            var configuration = new ToolboxConfiguration
            {
                Catalogs = normalizedCatalogs,
                MenuMode = NormalizeMenuMode(menuMode),
                IsPackage = isPackage
            };

            if (configuration.Catalogs.Count > 0)
            {
                configuration.Catalog = configuration.Catalogs[0];
                configuration.ToolboxPath = configuration.Catalogs[0].ToolboxPath;
            }
            else
            {
                configuration.Catalog = new ToolboxCatalog();
                configuration.ToolboxPath = string.Empty;
            }

            var directory = Path.GetDirectoryName(ConfigurationFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(configuration, SerializerOptions);
            File.WriteAllText(ConfigurationFilePath, json);
        }

        public static void Clear()
        {
            if (File.Exists(ConfigurationFilePath))
                File.Delete(ConfigurationFilePath);
        }

        private static void Normalize(ToolboxConfiguration configuration)
        {
            configuration.Catalogs ??= new List<ToolboxCatalog>();
            configuration.MenuMode = NormalizeMenuMode(configuration.MenuMode);

            if (configuration.Catalogs.Count == 0 && !string.IsNullOrWhiteSpace(configuration.Catalog?.ToolboxPath))
                configuration.Catalogs.Add(configuration.Catalog);

            if (configuration.Catalogs.Count == 0 && !string.IsNullOrWhiteSpace(configuration.ToolboxPath))
            {
                configuration.Catalogs.Add(new ToolboxCatalog
                {
                    ToolboxPath = configuration.ToolboxPath,
                    DisplayName = Path.GetFileNameWithoutExtension(configuration.ToolboxPath)
                });
            }

            configuration.Catalogs = NormalizeCatalogs(configuration.Catalogs);

            if (configuration.Catalogs.Count > 0)
            {
                configuration.Catalog = configuration.Catalogs[0];
                configuration.ToolboxPath = configuration.Catalogs[0].ToolboxPath;
            }
        }

        private static string NormalizeMenuMode(string menuMode)
        {
            return menuMode switch
            {
                "Toolbox" => "Toolbox",
                "Toolset" => "Toolset",
                _ => "Auto"
            };
        }

        private static string NormalizeCatalogMenuMode(string menuMode)
        {
            return menuMode switch
            {
                "Auto" => "Auto",
                "Toolbox" => "Toolbox",
                "Toolset" => "Toolset",
                "Tree" => "Tree",
                "Flat" => "Flat",
                _ => "Inherit"
            };
        }

        private static List<ToolboxCatalog> NormalizeCatalogs(IEnumerable<ToolboxCatalog> catalogs)
        {
            var normalized = new List<ToolboxCatalog>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var catalog in catalogs)
            {
                if (catalog is null)
                    continue;

                ToolboxCustomCatalogService.NormalizeCatalog(catalog);

                if (string.IsNullOrWhiteSpace(catalog.ToolboxPath) && !ToolboxCustomCatalogService.IsCustomCatalog(catalog))
                    continue;

                var key = BuildCatalogKey(catalog);
                if (!seenKeys.Add(key))
                    continue;

                normalized.Add(catalog);
            }

            return normalized;
        }

        private static string BuildCatalogKey(ToolboxCatalog catalog)
        {
            if (!string.IsNullOrWhiteSpace(catalog.ToolboxPath))
                return Path.GetFullPath(catalog.ToolboxPath);

            return $"{catalog.CatalogKind}:{catalog.DisplayName}";
        }
    }
}
