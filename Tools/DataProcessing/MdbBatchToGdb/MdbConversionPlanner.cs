using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal static class MdbConversionPlanner
    {
        public static List<MdbConversionPlan> BuildPlans(
            IEnumerable<MdbFileItem> selectedItems,
            bool saveToSourcePath,
            string inputFolderPath,
            string outputFolderPath,
            Action<string> onNameConflict)
        {
            var plans = new List<MdbConversionPlan>();
            var usedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (MdbFileItem item in selectedItems.OrderBy(x => x.FullPath, StringComparer.OrdinalIgnoreCase))
            {
                string preferredOutputPath = BuildPreferredOutputPath(item, saveToSourcePath, inputFolderPath, outputFolderPath);
                string finalOutputPath = BuildUniqueOutputPath(preferredOutputPath, usedOutputPaths);

                plans.Add(new MdbConversionPlan(item, finalOutputPath));

                if (!string.Equals(preferredOutputPath, finalOutputPath, StringComparison.OrdinalIgnoreCase))
                {
                    onNameConflict?.Invoke($"检测到输出重名，已自动重命名: {Path.GetFileName(finalOutputPath)}");
                }
            }

            return plans;
        }

        private static string BuildPreferredOutputPath(
            MdbFileItem item,
            bool saveToSourcePath,
            string inputFolderPath,
            string outputFolderPath)
        {
            string outputFolder = saveToSourcePath
                ? Path.GetDirectoryName(item.FullPath) ?? inputFolderPath
                : NormalizeOutputFolder(outputFolderPath);

            return Path.Combine(outputFolder, item.Name + ".gdb");
        }

        private static string NormalizeOutputFolder(string outputFolderPath)
        {
            if (string.IsNullOrWhiteSpace(outputFolderPath))
            {
                return outputFolderPath;
            }

            if (!outputFolderPath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
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
