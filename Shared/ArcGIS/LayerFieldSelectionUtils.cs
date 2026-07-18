using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Shared
{
    internal static class LayerFieldSelectionUtils
    {
        public static IReadOnlyList<FeatureLayer> GetPolygonLayers()
        {
            var map = MapView.Active?.Map;
            if (map == null)
            {
                return Array.Empty<FeatureLayer>();
            }

            return map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .Where(layer => layer.ShapeType == esriGeometryType.esriGeometryPolygon)
                .OrderBy(layer => layer.Name, StringComparer.CurrentCulture)
                .ToArray();
        }

        public static IReadOnlyList<FieldOption> GetWritableTextFields(FeatureLayer layer)
        {
            if (layer == null)
            {
                return Array.Empty<FieldOption>();
            }

            using var table = layer.GetTable();
            var fields = table.GetDefinition().GetFields();

            return fields
                .Where(field => field.FieldType == FieldType.String)
                .Where(field => !IsSystemField(field.Name))
                .OrderBy(field => field.Name, StringComparer.CurrentCulture)
                .Select(field => new FieldOption(field.Name, field.AliasName, field.Length))
                .ToArray();
        }

        private static bool IsSystemField(string fieldName)
        {
            return string.Equals(fieldName, "OBJECTID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "OID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "FID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "GLOBALID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "SHAPE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "SHAPE_LENGTH", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "SHAPE_AREA", StringComparison.OrdinalIgnoreCase);
        }
    }
}
