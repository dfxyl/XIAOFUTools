using System;
using System.Collections.Generic;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Core
{
    internal sealed record OvertureMfcTypeFolderInspection(
        string Name,
        int ParquetFileCount);

    internal sealed record OvertureMfcThemeFolderInspection(
        string Name,
        IReadOnlyList<OvertureMfcTypeFolderInspection> TypeFolders);

    internal sealed record OvertureMfcDataFolderInspection(
        bool Exists,
        int ParquetFileCount,
        IReadOnlyList<OvertureMfcThemeFolderInspection> ThemeFolders)
    {
        internal bool ContainsParquetFiles => ParquetFileCount > 0;

        internal static OvertureMfcDataFolderInspection Missing { get; } = new(
            Exists: false,
            ParquetFileCount: 0,
            ThemeFolders: Array.Empty<OvertureMfcThemeFolderInspection>());
    }
}
