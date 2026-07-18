using System;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.BoundaryPointGenerator
{
    internal partial class BoundaryPointGeneratorDockPaneViewModel
    {
        private async Task<string> CreateOutputFeatureClass()
        {
            try
            {
                var outputPath = OutputPath;
                if (string.IsNullOrEmpty(outputPath))
                {
                    LogError("输出路径为空");
                    return null;
                }

                SpatialReference spatialReference = null;
                if (SelectedPolygonLayer != null)
                {
                    using var table = SelectedPolygonLayer.GetTable();
                    if (table?.GetDefinition() is FeatureClassDefinition definition)
                    {
                        spatialReference = definition.GetSpatialReference();
                    }
                }

                var defaultName = SelectedPolygonLayer != null
                    ? $"{SelectedPolygonLayer.Name}_SZD"
                    : "四至坐标点SZD";
                var outputInfo = OutputDatasetUtils.ParseOutputPath(outputPath, defaultName);
                if (OutputDatasetUtils.Exists(outputInfo))
                {
                    var overwrite = false;
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        var message = outputInfo.IsGdb
                            ? $"目标要素类已存在：{outputInfo.CatalogPath}。是否覆盖？"
                            : $"目标Shapefile已存在：{outputInfo.CatalogPath}。是否覆盖？";
                        overwrite = PresentationServices.Dialogs.Show(
                            message,
                            "覆盖确认",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;
                    });
                    if (!overwrite)
                    {
                        LogWarning("用户取消覆盖，操作已中止。");
                        return null;
                    }

                    await OutputDatasetUtils.DeleteIfExistsAsync(outputInfo);
                    LogInfo($"删除已存在的数据集: {outputInfo.CatalogPath}");
                }

                var featureClassPath = await OutputDatasetUtils.CreateFeatureClassAsync(
                    outputInfo,
                    "POINT",
                    spatialReference);
                await AddFieldAsync(featureClassPath, "源要素ID", "LONG");
                await AddFieldAsync(featureClassPath, "方向", "TEXT", 10);
                await AddFieldAsync(featureClassPath, "X坐标_米", "DOUBLE");
                await AddFieldAsync(featureClassPath, "Y坐标_米", "DOUBLE");
                await AddSelectedFieldsAsync(featureClassPath);
                return featureClassPath;
            }
            catch (Exception ex)
            {
                LogError($"创建输出要素类时发生错误: {ex.Message}");
                return null;
            }
        }

        private async Task AddSelectedFieldsAsync(string featureClassPath)
        {
            if (SelectedPolygonLayer == null || SelectedFields == null || SelectedFields.Count == 0)
            {
                return;
            }

            using var table = SelectedPolygonLayer.GetTable();
            var sourceFields = table.GetDefinition().GetFields();
            foreach (var fieldName in SelectedFields)
            {
                var sourceField = sourceFields.FirstOrDefault(field => field.Name == fieldName);
                if (sourceField == null)
                {
                    continue;
                }

                var fieldType = GetGeoprocessingFieldType(sourceField.FieldType);
                await AddFieldAsync(
                    featureClassPath,
                    sourceField.Name,
                    fieldType,
                    sourceField.Length > 0 ? sourceField.Length : null);
                LogInfo($"添加保留字段: {sourceField.Name} ({fieldType})");
            }
        }

        private static Task AddFieldAsync(
            string featureClassPath,
            string fieldName,
            string fieldType,
            int? length = null)
        {
            var parameters = Geoprocessing.MakeValueArray(
                featureClassPath,
                fieldName,
                fieldType,
                null,
                null,
                length.HasValue ? length.Value : null);
            return Geoprocessing.ExecuteToolAsync("AddField_management", parameters);
        }
    }
}
