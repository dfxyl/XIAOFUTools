using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Features.DataManagement.MdbBatchToGdb
{
    internal static class MdbConversionPlanner
    {
        public static List<MdbConversionPlan> BuildPlans(
            IEnumerable<MdbFileItem> selectedItems,
            bool saveToSourcePath,
            string inputFolderPath,
            string outputFolderPath,
            MdbOutputFormat outputFormat,
            Action<string> onNameConflict)
        {
            List<MdbFileItem> orderedItems = selectedItems
                .OrderBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            IReadOnlyList<MdbOutputPathPlan> outputPaths = MdbOutputPathPlanner.BuildPaths(
                orderedItems.Select(item => new MdbOutputPathRequest(item.FullPath, item.Name)),
                saveToSourcePath,
                inputFolderPath,
                outputFolderPath,
                outputFormat);
            var plans = new List<MdbConversionPlan>(orderedItems.Count);

            for (int index = 0; index < orderedItems.Count; index++)
            {
                MdbOutputPathPlan outputPath = outputPaths[index];
                plans.Add(new MdbConversionPlan(orderedItems[index], outputPath.OutputPath, outputFormat));

                if (!string.Equals(outputPath.PreferredPath, outputPath.OutputPath, StringComparison.OrdinalIgnoreCase))
                {
                    onNameConflict?.Invoke($"检测到输出重名，已自动重命名: {Path.GetFileName(outputPath.OutputPath)}");
                }
            }

            return plans;
        }
    }
}
