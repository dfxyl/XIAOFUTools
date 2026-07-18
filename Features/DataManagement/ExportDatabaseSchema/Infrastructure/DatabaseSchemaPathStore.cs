using System;
using System.IO;

namespace XIAOFUTools.Features.DataManagement.ExportDatabaseSchema.Infrastructure
{
    /// <summary>
    /// 负责数据库结构导出功能的本地路径验证。
    /// </summary>
    internal sealed class DatabaseSchemaPathStore
    {
        internal bool DirectoryExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
    }
}
