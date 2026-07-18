using System;
using System.IO;

namespace XIAOFUTools.Features.User.Settings.Infrastructure
{
    /// <summary>
    /// 负责预设图层目录和图层文件的本地操作。
    /// </summary>
    internal sealed class PresetLayerFileStore
    {
        internal void EnsureDirectory(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            Directory.CreateDirectory(path);
        }

        internal bool FileExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && File.Exists(path);

        internal void CopyFile(string sourcePath, string destinationPath) =>
            File.Copy(sourcePath, destinationPath, overwrite: true);
    }
}
