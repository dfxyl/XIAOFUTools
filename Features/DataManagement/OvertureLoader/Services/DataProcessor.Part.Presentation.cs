using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using DuckDB.NET.Data;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Services
{
    public partial class DataProcessor
    {

        /// <summary>
        /// 更新缓存
        /// </summary>
        private static void UpdateCache(string key, int rowCount = 0)
        {
            _schemaCache[key] = DateTime.Now;
            if (rowCount > 0)
            {
                _rowCountCache[key] = rowCount;
            }
        }

        /// <summary>
        /// Fallback method for individual layer creation if bulk creation fails
        /// </summary>
        private async Task FallbackToIndividualLayerCreation(List<LayerCreationInfo> layers, IProgress<string> progress)
        {
            foreach (var layerInfo in layers)
            {
                try
                {
                    await AddLayerToMapAsync(layerInfo.FilePath, layerInfo.LayerName, progress);
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error adding layer {layerInfo.LayerName}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Error in fallback layer creation for {layerInfo.LayerName}: {ex.Message}");
                }
            }
        }
    }
}
