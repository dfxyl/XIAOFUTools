using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Services
{
    public partial class DataProcessor
    {

        public async Task<DataTable> GetPreviewDataAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _duckDbDataSession.GetPreviewDataAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get preview data: {ex.Message}", ex);
            }
        }

        // Helper method to get a more descriptive geometry type name
        private static string GetDescriptiveGeometryType(string geomType) => geomType switch
        {
            "POINT" => "points",
            "LINESTRING" => "lines",
            "POLYGON" => "polygons",
            "MULTIPOINT" => "multipoints",
            "MULTILINESTRING" => "multilines",
            "MULTIPOLYGON" => "multipolygons",
            _ => geomType.ToLowerInvariant()
        };
    }
}
