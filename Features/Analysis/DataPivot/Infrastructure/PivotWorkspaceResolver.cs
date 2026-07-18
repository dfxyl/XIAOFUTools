using System;
using System.IO;

namespace XIAOFUTools.Features.Analysis.DataPivot.Infrastructure
{
    /// <summary>
    /// 负责数据透视临时地理数据库的本地路径选择与可用性检查。
    /// ArcGIS 项目路径由调用方提供，避免基础设施持有 ArcGIS 宿主对象。
    /// </summary>
    internal sealed class PivotWorkspaceResolver
    {
        internal string ResolveTemporaryWorkspacePath(string outputGeodatabasePath, string projectDefaultGeodatabasePath)
        {
            if (IsUsableGeodatabaseWorkspace(outputGeodatabasePath))
            {
                return outputGeodatabasePath;
            }

            if (IsUsableGeodatabaseWorkspace(projectDefaultGeodatabasePath))
            {
                return projectDefaultGeodatabasePath;
            }

            return outputGeodatabasePath;
        }

        internal bool DirectoryExists(string folderPath) =>
            !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);

        private bool IsUsableGeodatabaseWorkspace(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            path.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase) &&
            DirectoryExists(path);
    }
}
