using System;
using System.IO;

namespace XIAOFUTools.Features.DataManagement.ProtocolLineExtract.Infrastructure
{
    /// <summary>
    /// 管理协议线提取过程 Shapefile 的临时工作空间。
    /// </summary>
    internal sealed class ProtocolLineTemporaryWorkspaceStore
    {
        internal string CreateWorkspace()
        {
            string path = Path.Combine(Path.GetTempPath(), "ProtocolLine_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(path);
            return path;
        }

        internal void DeleteIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
