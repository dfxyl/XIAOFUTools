using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Features.DataManagement.MdbBatchToGdb
{
    internal readonly record struct MdbOutputPathRequest(string SourcePath, string Name);

    internal readonly record struct MdbOutputPathPlan(string SourcePath, string PreferredPath, string OutputPath);

    internal static class MdbOutputPathPlanner
    {
        public static IReadOnlyList<MdbOutputPathPlan> BuildPaths(
            IEnumerable<MdbOutputPathRequest> requests,
            bool saveToSourcePath,
            string inputFolderPath,
            string outputFolderPath,
            MdbOutputFormat outputFormat)
        {
            var plans = new List<MdbOutputPathPlan>();
            var usedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (MdbOutputPathRequest request in requests.OrderBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase))
            {
                string preferredPath = BuildPreferredOutputPath(
                    request,
                    saveToSourcePath,
                    inputFolderPath,
                    outputFolderPath,
                    outputFormat);
                string outputPath = BuildUniqueOutputPath(preferredPath, usedOutputPaths);
                plans.Add(new MdbOutputPathPlan(request.SourcePath, preferredPath, outputPath));
            }

            return plans;
        }

        private static string BuildPreferredOutputPath(
            MdbOutputPathRequest request,
            bool saveToSourcePath,
            string inputFolderPath,
            string outputFolderPath,
            MdbOutputFormat outputFormat)
        {
            string outputFolder = saveToSourcePath
                ? Path.GetDirectoryName(request.SourcePath) ?? inputFolderPath
                : NormalizeOutputFolder(outputFolderPath);

            return Path.Combine(outputFolder, request.Name + outputFormat.GetExtension());
        }

        public static string NormalizeOutputFolder(string outputFolderPath)
        {
            if (string.IsNullOrWhiteSpace(outputFolderPath))
            {
                return outputFolderPath;
            }

            if (!outputFolderPath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase) &&
                !outputFolderPath.EndsWith(".geodatabase", StringComparison.OrdinalIgnoreCase) &&
                !outputFolderPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return outputFolderPath;
            }

            return Path.GetDirectoryName(outputFolderPath) ?? outputFolderPath;
        }

        private static string BuildUniqueOutputPath(string preferredOutputPath, ISet<string> usedOutputPaths)
        {
            string folder = Path.GetDirectoryName(preferredOutputPath) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(preferredOutputPath);
            string extension = Path.GetExtension(preferredOutputPath);

            string candidate = preferredOutputPath;
            int suffix = 2;
            while (usedOutputPaths.Contains(candidate))
            {
                candidate = Path.Combine(folder, $"{baseName}_{suffix}{extension}");
                suffix++;
            }

            usedOutputPaths.Add(candidate);
            return candidate;
        }
    }
}
