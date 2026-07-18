#nullable enable

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace XIAOFUTools.Features.General.InternetTileDownload.Services
{
    internal static class InternetTileOutputPathBuilder
    {
        private static readonly Regex InvalidFileNameRegex = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Compiled);

        public static string BuildForFolder(string outputFolderPath, string serviceUrl, string levelId)
        {
            if (string.IsNullOrWhiteSpace(outputFolderPath))
            {
                return outputFolderPath;
            }

            var hostText = "internet_tiles";
            if (Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri))
            {
                hostText = InvalidFileNameRegex.Replace(uri.Host.Replace('.', '_'), "_").Trim('_');
            }

            var normalizedLevel = InvalidFileNameRegex.Replace(levelId ?? string.Empty, "_");
            var fileName = string.IsNullOrWhiteSpace(normalizedLevel)
                ? $"{hostText}.tif"
                : $"{hostText}_L{normalizedLevel}.tif";
            return Path.Combine(outputFolderPath, fileName);
        }
    }
}
