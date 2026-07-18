using System;
using System.Collections.Generic;
using System.IO;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Core
{
    internal static class OvertureDataFolderPlanner
    {
        public static IReadOnlyList<string> BuildThemeFolders(
            string dataRoot,
            IEnumerable<string> actualTypes)
        {
            if (string.IsNullOrWhiteSpace(dataRoot))
            {
                throw new ArgumentException("数据根目录不能为空。", nameof(dataRoot));
            }
            ArgumentNullException.ThrowIfNull(actualTypes);

            var normalizedRoot = Path.GetFullPath(dataRoot);
            var folders = new List<string>();
            var seenTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var actualType in actualTypes)
            {
                if (string.IsNullOrWhiteSpace(actualType))
                {
                    continue;
                }

                var normalizedType = actualType.Trim();
                if (normalizedType is "." or ".." ||
                    normalizedType.IndexOfAny(new[]
                    {
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    }) >= 0 ||
                    normalizedType.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new ArgumentException($"无效的 Overture 数据类型：{actualType}", nameof(actualTypes));
                }

                if (seenTypes.Add(normalizedType))
                {
                    folders.Add(Path.Combine(normalizedRoot, normalizedType));
                }
            }

            return folders;
        }
    }
}
