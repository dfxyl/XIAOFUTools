using System;
using System.IO;

namespace XIAOFUTools.Features.DataManagement.MirrorDatabase.Infrastructure
{
    /// <summary>
    /// 负责镜像数据库输入和输出路径的可用性验证。
    /// </summary>
    internal sealed class MirrorDatabasePathStore
    {
        internal bool DirectoryExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
    }
}
