using System;
using System.IO;

namespace XIAOFUTools.Features.DataManagement.ShapefileBuilder.Infrastructure
{
    /// <summary>
    /// 负责建 SHP 模板、输入文件和输出目录的本地文件系统操作。
    /// </summary>
    internal sealed class ShapefileBuilderFileStore
    {
        internal bool FileExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && File.Exists(path);

        internal bool DirectoryExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

        internal void EnsureDirectory(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            Directory.CreateDirectory(path);
        }

        internal void CopyFile(string sourcePath, string destinationPath) =>
            File.Copy(sourcePath, destinationPath, overwrite: true);
    }
}
