using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using DuckDB.NET.Data;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;
using XIAOFUTools.Shared.Diagnostics;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Services
{
    // Structure to hold layer information for bulk creation
    public class LayerCreationInfo
    {
        public string FilePath { get; set; }
        public string LayerName { get; set; }
        public string GeometryType { get; set; }
        public int StackingPriority { get; set; }
        public string ParentTheme { get; set; }
        public string ActualType { get; set; }
    }

    public partial class DataProcessor : IDisposable
    {
        private readonly DuckDBConnection _connection;
        private readonly IOvertureMapMemberCleanupService _mapMemberCleanupService;
        private readonly IOvertureDuckDbInitializer _duckDbInitializer;
        private readonly IOvertureDuckDbDataSession _duckDbDataSession;
        private readonly IOvertureGeoParquetExporter _geoParquetExporter;

        // Constants
        private const string DEFAULT_SRS = "EPSG:4326";
        private const string STRUCT_TYPE = "STRUCT";
        private const string BBOX_COLUMN = "bbox";
        private const string GEOMETRY_COLUMN = "geometry";

        // Add static readonly field for the theme-type separator
        private static readonly string[] THEME_TYPE_SEPARATOR = [" - "];

        // 缓存相关字段
        private static readonly Dictionary<string, DateTime> _schemaCache = new();
        private static readonly Dictionary<string, int> _rowCountCache = new();
        private static readonly TimeSpan CACHE_EXPIRY = TimeSpan.FromMinutes(30);

        // Fields to store theme context for file path generation
        private string _currentParentS3Theme;
        private string _currentActualS3Type;

        // Collection to store layer information for bulk creation
        private readonly List<LayerCreationInfo> _pendingLayers;

        public DataProcessor()
            : this(new ArcGisOvertureMapMemberCleanupService())
        {
        }

        internal DataProcessor(IOvertureMapMemberCleanupService mapMemberCleanupService)
        {
            _mapMemberCleanupService = mapMemberCleanupService ??
                throw new ArgumentNullException(nameof(mapMemberCleanupService));
            _connection = new DuckDBConnection("DataSource=:memory:");
            _duckDbDataSession = new OvertureDuckDbDataSession(_connection);
            _duckDbInitializer = new OvertureDuckDbInitializer(
                new DuckDbConnectionCommandExecutor(_connection),
                new FileSystemOvertureDuckDbExtensionCatalog(),
                new AppLogger(),
                Assembly.GetExecutingAssembly().Location);
            _geoParquetExporter = new OvertureGeoParquetExporter(
                _duckDbDataSession,
                _mapMemberCleanupService);
            _pendingLayers = new List<LayerCreationInfo>();
        }
    }
}
