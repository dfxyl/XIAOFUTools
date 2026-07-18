using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Core
{
    internal static class OvertureMfcSchemaPlanner
    {
        public const string GeometryColumn = "geometry";
        public const int DefaultWkid = 4326;

        private static readonly string[] RequiredBboxFields =
        {
            "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax"
        };

        private static readonly HashSet<string> KnownBooleanFields =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "has_parts", "is_underground", "is_land", "is_territorial",
                "is_salt", "is_intermittent"
            };

        private static readonly IReadOnlyDictionary<string, HashSet<string>> FieldExclusions =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["address"] = NewSet("address_levels", "sources"),
                ["building"] = NewSet("sources"),
                ["building_part"] = NewSet("sources"),
                ["connector"] = NewSet("sources"),
                ["division"] = NewSet(
                    "sources", "local_type", "hierarchies",
                    "capital_division_ids", "capital_of_divisions"),
                ["division_area"] = NewSet("sources"),
                ["infrastructure"] = NewSet("sources", "source_tags"),
                ["land"] = NewSet("sources", "source_tags"),
                ["land_cover"] = NewSet("sources"),
                ["land_use"] = NewSet("sources", "source_tags"),
                ["place"] = NewSet(
                    "addresses", "brand", "emails", "phones", "socials",
                    "sources", "websites"),
                ["segment"] = NewSet(
                    "access_restrictions", "connectors", "destinations", "level_rules",
                    "prohibited_transitions", "road_flags", "road_surface", "routes",
                    "sources", "speed_limits", "subclass_rules", "width_rules"),
                ["water"] = NewSet("sources", "source_tags")
            };

        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>
            FieldRenames = new Dictionary<string, IReadOnlyDictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["division"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["perspectives"] = "perspectives_mode",
                    ["norms"] = "norms_driving_side"
                }
            };

        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DatasetFieldOrder =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["address"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "country", "postcode", "street", "number", "unit", "postal_city",
                    "version", "filename", "theme", "type", "geometry"),
                ["building"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "version", "level", "subtype", "class", "height", "names_primary",
                    "has_parts", "is_underground", "num_floors", "num_floors_underground",
                    "min_height", "min_floor", "facade_color", "facade_material",
                    "roof_material", "roof_shape", "roof_direction", "roof_orientation",
                    "roof_color", "roof_height", "filename", "theme", "type", "geometry"),
                ["building_part"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "version", "level", "height", "names_primary", "is_underground",
                    "num_floors", "num_floors_underground", "min_height", "min_floor",
                    "facade_color", "facade_material", "roof_material", "roof_shape",
                    "roof_direction", "roof_orientation", "roof_color", "roof_height",
                    "building_id", "filename", "theme", "type", "geometry"),
                ["connector"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "version", "filename", "theme", "type", "geometry"),
                ["division"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "country", "version", "cartography_prominence", "cartography_min_zoom",
                    "cartography_max_zoom", "cartography_sort_key", "subtype", "class",
                    "names_primary", "wikidata", "region", "perspectives_mode",
                    "parent_division_id", "norms_driving_side", "population",
                    "filename", "theme", "type", "geometry"),
                ["division_area"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "country", "version", "subtype", "class", "names_primary",
                    "is_land", "is_territorial", "region", "division_id",
                    "filename", "theme", "type", "geometry"),
                ["infrastructure"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "version", "level", "subtype", "class", "height", "surface",
                    "names_primary", "wikidata", "filename", "theme", "type", "geometry"),
                ["land"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "version", "level", "subtype", "class", "surface", "names_primary",
                    "wikidata", "elevation", "filename", "theme", "type", "geometry"),
                ["land_cover"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "version", "cartography_prominence", "cartography_min_zoom",
                    "cartography_max_zoom", "cartography_sort_key", "subtype",
                    "filename", "theme", "type", "geometry"),
                ["land_use"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "version", "level", "subtype", "class", "surface", "names_primary",
                    "wikidata", "filename", "theme", "type", "geometry"),
                ["place"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "version", "names_primary", "categories_primary", "confidence",
                    "brand_wikidata", "brand_names_primary", "filename", "theme", "type",
                    "geometry"),
                ["segment"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "version", "subtype", "class", "names_primary", "subclass",
                    "filename", "theme", "type", "geometry"),
                ["water"] = Fields(
                    "id", "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax",
                    "version", "level", "subtype", "class", "names_primary", "wikidata",
                    "is_salt", "is_intermittent", "filename", "theme", "type", "geometry")
            };

        public static string BuildDescribeSql(string parquetPath)
        {
            return $"DESCRIBE SELECT * FROM read_parquet('{EscapePath(parquetPath)}') LIMIT 0;";
        }

        public static string BuildGeometryTypeSql(string parquetPath)
        {
            return $"SELECT ST_GeometryType({GeometryColumn}) " +
                   $"FROM read_parquet('{EscapePath(parquetPath)}') " +
                   $"WHERE {GeometryColumn} IS NOT NULL LIMIT 1;";
        }

        public static OvertureMfcDataset PlanDataset(
            string datasetName,
            IReadOnlyList<OvertureDuckDbColumn> schema,
            string? detectedGeometryType,
            Action<string>? log = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(datasetName);
            ArgumentNullException.ThrowIfNull(schema);

            var fields = new List<OvertureMfcField>();
            var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasGeometry = false;

            foreach (var column in schema)
            {
                var columnName = column.Name;
                var duckDbType = column.Type.ToUpperInvariant();
                log?.Invoke(
                    $"MFC Generation: Dataset '{datasetName}' - Schema Column: " +
                    $"'{columnName}', DuckDB Type: '{duckDbType}'");

                if (columnName.StartsWith("__duckdb_internal", StringComparison.Ordinal))
                {
                    continue;
                }

                if (FieldExclusions.TryGetValue(datasetName, out var exclusions) &&
                    exclusions.Contains(columnName))
                {
                    log?.Invoke(
                        $"MFC Generation: Excluding field '{columnName}' for dataset " +
                        $"'{datasetName}' as per exclusion rules.");
                    continue;
                }

                if (FieldRenames.TryGetValue(datasetName, out var renames) &&
                    renames.TryGetValue(columnName, out var renamedColumn))
                {
                    log?.Invoke(
                        $"MFC Generation: Renaming field '{columnName}' to " +
                        $"'{renamedColumn}' for dataset '{datasetName}'.");
                    columnName = renamedColumn;
                }

                if (columnName.Equals(GeometryColumn, StringComparison.OrdinalIgnoreCase))
                {
                    hasGeometry = true;
                    AddField(fields, addedNames, new OvertureMfcField(columnName, "Binary"));
                    continue;
                }

                if (RequiredBboxFields.Contains(columnName, StringComparer.OrdinalIgnoreCase))
                {
                    AddField(fields, addedNames, new OvertureMfcField(columnName, "Float32"));
                    continue;
                }

                if (columnName.Equals("bbox", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if ((columnName.Equals("names", StringComparison.OrdinalIgnoreCase) ||
                     columnName.Equals("categories", StringComparison.OrdinalIgnoreCase)) &&
                    duckDbType.StartsWith("STRUCT", StringComparison.Ordinal))
                {
                    AddField(
                        fields,
                        addedNames,
                        new OvertureMfcField($"{columnName}_primary", "String"));
                    continue;
                }

                if (columnName.Equals("cartography", StringComparison.OrdinalIgnoreCase) &&
                    duckDbType.StartsWith("STRUCT", StringComparison.Ordinal))
                {
                    AddField(fields, addedNames, new("cartography_prominence", "Int32"));
                    AddField(fields, addedNames, new("cartography_min_zoom", "Int32"));
                    AddField(fields, addedNames, new("cartography_max_zoom", "Int32"));
                    AddField(fields, addedNames, new("cartography_sort_key", "Int32"));
                    continue;
                }

                var mfcType = ConvertDuckDbType(duckDbType, columnName, log);
                var sourceType = KnownBooleanFields.Contains(columnName) ? "Boolean" : null;
                if (sourceType != null)
                {
                    mfcType = "String";
                }

                AddField(
                    fields,
                    addedNames,
                    new OvertureMfcField(columnName, mfcType, SourceType: sourceType));
            }

            foreach (var bboxField in RequiredBboxFields)
            {
                AddField(fields, addedNames, new OvertureMfcField(bboxField, "Float32"));
            }

            if (datasetName.Equals("place", StringComparison.OrdinalIgnoreCase))
            {
                AddField(fields, addedNames, new("brand_wikidata", "String"));
                AddField(fields, addedNames, new("brand_names_primary", "String"));
            }

            return new OvertureMfcDataset
            {
                Name = datasetName,
                Alias = datasetName,
                Fields = OrderFields(datasetName, fields, log),
                Geometry = BuildGeometry(hasGeometry, detectedGeometryType)
            };
        }

        private static OvertureMfcGeometry? BuildGeometry(
            bool hasGeometry,
            string? geometryType)
        {
            if (!hasGeometry)
            {
                return null;
            }

            return new OvertureMfcGeometry
            {
                GeometryType = string.IsNullOrWhiteSpace(geometryType)
                    ? "esriGeometryAny"
                    : OvertureGeometryTypeMapper.ToEsriGeometryType(geometryType),
                SpatialReference = new OvertureMfcSpatialReference { Wkid = DefaultWkid },
                Fields = [new OvertureMfcGeometryField()]
            };
        }

        private static IReadOnlyList<OvertureMfcField> OrderFields(
            string datasetName,
            IReadOnlyList<OvertureMfcField> fields,
            Action<string>? log)
        {
            if (!DatasetFieldOrder.TryGetValue(datasetName, out var requestedOrder))
            {
                return OrderDefault(fields);
            }

            var available = fields.ToDictionary(
                field => field.Name,
                StringComparer.OrdinalIgnoreCase);
            var ordered = new List<OvertureMfcField>();
            foreach (var fieldName in requestedOrder)
            {
                if (available.Remove(fieldName, out var field))
                {
                    ordered.Add(field);
                }
                else
                {
                    log?.Invoke(
                        $"Warning: Field '{fieldName}' specified in order for dataset " +
                        $"'{datasetName}' was not found in available fields.");
                }
            }

            var geometry = available.Remove(GeometryColumn, out var geometryField)
                ? geometryField
                : null;
            ordered.AddRange(available.Values.OrderBy(
                field => field.Name,
                StringComparer.OrdinalIgnoreCase));
            if (geometry != null &&
                !requestedOrder.Contains(GeometryColumn, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(geometry);
            }

            return ordered;
        }

        private static IReadOnlyList<OvertureMfcField> OrderDefault(
            IReadOnlyList<OvertureMfcField> fields)
        {
            var available = fields.ToDictionary(
                field => field.Name,
                StringComparer.OrdinalIgnoreCase);
            var ordered = new List<OvertureMfcField>();
            AddIfAvailable("id", available, ordered);
            foreach (var bboxField in RequiredBboxFields)
            {
                AddIfAvailable(bboxField, available, ordered);
            }

            var geometry = available.Remove(GeometryColumn, out var geometryField)
                ? geometryField
                : null;
            ordered.AddRange(available.Values.OrderBy(
                field => field.Name,
                StringComparer.OrdinalIgnoreCase));
            if (geometry != null)
            {
                ordered.Add(geometry);
            }

            return ordered;
        }

        private static string ConvertDuckDbType(
            string duckDbType,
            string columnName,
            Action<string>? log)
        {
            if (duckDbType.StartsWith("DECIMAL", StringComparison.Ordinal))
            {
                return "Float64";
            }

            if (duckDbType.StartsWith("VARCHAR", StringComparison.Ordinal) ||
                duckDbType.Contains("CHAR", StringComparison.Ordinal) ||
                duckDbType == "TEXT")
            {
                return "String";
            }

            return duckDbType switch
            {
                "BOOLEAN" => "String",
                "TINYINT" => "Int8",
                "SMALLINT" => "Int16",
                "INTEGER" => "Int32",
                "BIGINT" => "Int64",
                "HUGEINT" => "String",
                "FLOAT4" or "REAL" or "FLOAT" => "Float32",
                "FLOAT8" or "DOUBLE PRECISION" or "DOUBLE" => "Float64",
                "DATE" => "Date",
                "TIMESTAMP" or "TIMESTAMPTZ" or "TIME" or "INTERVAL" => "String",
                "BLOB" or "BYTEA" => "Binary",
                _ => LogUnknownTypeAndUseString(duckDbType, columnName, log)
            };
        }

        private static string LogUnknownTypeAndUseString(
            string duckDbType,
            string columnName,
            Action<string>? log)
        {
            var isComplex = duckDbType.StartsWith("STRUCT", StringComparison.Ordinal) ||
                            duckDbType.StartsWith("LIST", StringComparison.Ordinal) ||
                            duckDbType.StartsWith("ARRAY", StringComparison.Ordinal) ||
                            duckDbType.StartsWith("MAP", StringComparison.Ordinal);
            log?.Invoke(isComplex
                ? $"Warning: Converting complex DuckDB type '{duckDbType}' for column " +
                  $"'{columnName}' to String. Data may be stringified."
                : $"Warning: Unknown DuckDB type for column '{columnName}': " +
                  $"'{duckDbType}'. Defaulting to String.");
            return "String";
        }

        private static string EscapePath(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return Path.GetFullPath(path)
                .Replace('\\', '/')
                .Replace("'", "''", StringComparison.Ordinal);
        }

        private static void AddField(
            ICollection<OvertureMfcField> fields,
            ISet<string> addedNames,
            OvertureMfcField field)
        {
            if (addedNames.Add(field.Name))
            {
                fields.Add(field);
            }
        }

        private static void AddIfAvailable(
            string fieldName,
            IDictionary<string, OvertureMfcField> available,
            ICollection<OvertureMfcField> ordered)
        {
            if (available.Remove(fieldName, out var field))
            {
                ordered.Add(field);
            }
        }

        private static HashSet<string> NewSet(params string[] values) =>
            new(values, StringComparer.OrdinalIgnoreCase);

        private static IReadOnlyList<string> Fields(params string[] fields) => fields;
    }
}
