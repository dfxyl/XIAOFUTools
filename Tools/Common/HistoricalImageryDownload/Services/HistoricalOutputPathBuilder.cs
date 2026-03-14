#nullable enable

using System.IO;
using System.Text.RegularExpressions;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    public static class HistoricalOutputPathBuilder
    {
        private static readonly Regex DateSuffixRegex = new(@"_\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

        public static string BuildForFolder(string outputFolderPath, HistoricalVersionItem version, int zoomLevel)
        {
            if (string.IsNullOrWhiteSpace(outputFolderPath))
            {
                return outputFolderPath;
            }

            var dateText = !string.IsNullOrWhiteSpace(version.AcquisitionDate)
                ? version.AcquisitionDate!
                : version.DisplayDate;
            var providerText = version.Provider == HistoricalImageryProviderType.GoogleEarth ? "Google" : "Wayback";
            var fileName = $"{providerText}_{dateText}_Z{zoomLevel}.tif";
            return Path.Combine(outputFolderPath, fileName);
        }

        public static string Build(string basePath, HistoricalVersionItem version)
        {
            var dateText = !string.IsNullOrWhiteSpace(version.AcquisitionDate)
                ? version.AcquisitionDate!
                : version.DisplayDate;

            if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(dateText))
            {
                return basePath;
            }

            var directory = Path.GetDirectoryName(basePath) ?? string.Empty;
            var extension = Path.GetExtension(basePath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(basePath);
            var normalizedFileName = DateSuffixRegex.Replace(fileNameWithoutExtension, string.Empty);
            var datedFileName = $"{normalizedFileName}_{dateText}{extension}";
            return Path.Combine(directory, datedFileName);
        }
    }
}
