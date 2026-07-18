using System;
using System.Collections.Generic;
using System.IO;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure
{
    internal sealed class FileSystemOvertureDuckDbExtensionCatalog : IOvertureDuckDbExtensionCatalog
    {
        public IReadOnlyList<string> GetExtensionFiles(string extensionDirectory)
        {
            if (string.IsNullOrWhiteSpace(extensionDirectory) ||
                !Directory.Exists(extensionDirectory))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(
                extensionDirectory,
                "*.duckdb_extension",
                SearchOption.TopDirectoryOnly);
        }
    }
}
