using System;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Core
{
    internal static class OvertureGeometryTypeMapper
    {
        public static string ToEsriGeometryType(
            string duckDbGeometryType,
            Action<string>? log = null)
        {
            var normalizedType = duckDbGeometryType?.Trim().ToUpperInvariant();
            return normalizedType switch
            {
                "POINT" or "MULTIPOINT" => "esriGeometryPoint",
                "LINESTRING" or "MULTILINESTRING" => "esriGeometryPolyline",
                "POLYGON" or "MULTIPOLYGON" => "esriGeometryPolygon",
                _ => LogUnknownAndUsePoint(normalizedType, log)
            };
        }

        private static string LogUnknownAndUsePoint(
            string? geometryType,
            Action<string>? log)
        {
            log?.Invoke(
                $"Unmapped DuckDB geometry type: {geometryType}. " +
                "Defaulting to esriGeometryPoint.");
            return "esriGeometryPoint";
        }
    }
}
