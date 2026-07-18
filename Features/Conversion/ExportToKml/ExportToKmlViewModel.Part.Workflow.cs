using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO.Compression;
using System.Xml;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Conversion.ExportToKml
{
    internal partial class ExportToKmlViewModel
    {

        private async Task ExecuteLayerToKmlAsync(
            object layerInput,
            string outputKmzPath,
            CancellationToken cancellationToken,
            string whereClause = null)
        {
            var env = Geoprocessing.MakeEnvironmentArray("overwriteoutput", "True", "addOutputsToMap", "False");
            var primaryParams = Geoprocessing.MakeValueArray(layerInput, outputKmzPath);

            var featureLayer = layerInput as FeatureLayer;
            if (featureLayer != null && !string.IsNullOrWhiteSpace(whereClause))
            {
                await SelectSourceLayerByWhereAsync(featureLayer, whereClause, cancellationToken);
            }

            try
            {
                var primaryResult = await Geoprocessing.ExecuteToolAsync(
                    LayerToKmlToolName,
                    primaryParams,
                    env,
                    cancellationToken,
                    null,
                    GPExecuteToolFlags.None);

                if (primaryResult != null && !primaryResult.IsFailed)
                {
                    return;
                }

                AddLog($"工具 {LayerToKmlToolName} 调用失败，尝试兼容名称 {LayerToKmlLegacyToolName}");

                var fallbackParams = Geoprocessing.MakeValueArray(layerInput, outputKmzPath);
                var fallbackResult = await Geoprocessing.ExecuteToolAsync(
                    LayerToKmlLegacyToolName,
                    fallbackParams,
                    env,
                    cancellationToken,
                    null,
                    GPExecuteToolFlags.None);

                if (fallbackResult == null || fallbackResult.IsFailed)
                {
                    var message = BuildGpErrorMessage(fallbackResult ?? primaryResult);
                    throw new InvalidOperationException($"内置Layer To KML执行失败: {message}");
                }
            }
            finally
            {
                if (featureLayer != null && !string.IsNullOrWhiteSpace(whereClause))
                {
                    await ClearSourceLayerSelectionAsync(featureLayer);
                }
            }
        }
    }
}
