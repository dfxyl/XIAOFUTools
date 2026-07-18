using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.Analysis.MultiOverlaySummary.Infrastructure
{
    internal interface IMultiOverlayExcelExporter
    {
        Task ExportAsync(DataTable resultTable, int decimalPlaces, string filePath, CancellationToken cancellationToken);
    }
}
