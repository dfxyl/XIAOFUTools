using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.ExportDatabaseSchema.Core;

namespace XIAOFUTools.Features.DataManagement.ExportDatabaseSchema.Infrastructure
{
    internal interface IDatabaseSchemaExcelExporter
    {
        Task ExportAsync(
            IReadOnlyList<FeatureDatasetInfo> featureDatasets,
            IReadOnlyList<FeatureClassInfo> featureClasses,
            string outputPath,
            CancellationToken cancellationToken);
    }
}
