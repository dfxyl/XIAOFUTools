using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using XIAOFUTools.Features.Cartography.MapSeriesExport.Core;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport.Infrastructure
{
    internal sealed class MapSeriesSettingsStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _settingsPath;

        internal MapSeriesSettingsStore(string settingsPath = null)
        {
            _settingsPath = settingsPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIAOFUTools",
                "MapSeriesSettings.json");
        }

        internal CoordinateTableSettings Load()
        {
            try
            {
                return File.Exists(_settingsPath)
                    ? JsonSerializer.Deserialize<CoordinateTableSettings>(
                        File.ReadAllText(_settingsPath),
                        SerializerOptions)
                    : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载地图系列设置失败: {ex.Message}");
                return null;
            }
        }

        internal void Save(CoordinateTableSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(
                    _settingsPath,
                    JsonSerializer.Serialize(settings, SerializerOptions));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存地图系列设置失败: {ex.Message}");
            }
        }
    }
}
