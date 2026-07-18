using System;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Features.DataManagement.BatchAddData.Infrastructure
{
    /// <summary>
    /// 验证批量添加数据功能的本地搜索根路径。
    /// </summary>
    internal sealed class BatchAddDataSearchPathValidator
    {
        internal bool IsAccessible(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                if ((path.Length == 3 && path.EndsWith(@":\")) || (path.Length == 2 && path.EndsWith(":")))
                {
                    string normalizedPath = path.EndsWith(@"\") ? path : path + @"\";
                    var drive = DriveInfo.GetDrives().FirstOrDefault(item =>
                        item.Name.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
                    return drive?.IsReady == true;
                }

                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }
    }
}
