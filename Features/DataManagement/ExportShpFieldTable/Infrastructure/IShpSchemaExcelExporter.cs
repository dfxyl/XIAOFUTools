using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.ExportShpFieldTable.Core;

namespace XIAOFUTools.Features.DataManagement.ExportShpFieldTable.Infrastructure
{
    internal interface IShpSchemaExcelExporter
    {
        Task ExportAsync(
            IReadOnlyList<ShpLayerSchemaInfo> layers,
            string outputPath,
            CancellationToken cancellationToken);
    }
}
