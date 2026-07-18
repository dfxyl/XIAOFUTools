using System;
using System.IO;

namespace XIAOFUTools.Shared.IO.Cad
{
    /// <summary>
    /// 提供 CAD 文件输出的桌面默认目录与可用目录判断。
    /// </summary>
    internal sealed class CadOutputPathResolver
    {
        internal string GetDesktopDirectoryOrNull()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            return !string.IsNullOrWhiteSpace(desktop) && Directory.Exists(desktop)
                ? desktop
                : null;
        }

        internal string CreateDefaultOutputPath(string fileName)
        {
            var desktop = GetDesktopDirectoryOrNull();
            return string.IsNullOrWhiteSpace(desktop)
                ? null
                : Path.Combine(desktop, fileName);
        }
    }
}
