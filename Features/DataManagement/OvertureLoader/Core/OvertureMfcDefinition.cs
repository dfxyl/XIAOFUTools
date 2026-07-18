using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Core
{
    internal sealed class OvertureMfcDefinition
    {
        [JsonPropertyName("connection")]
        public OvertureMfcConnection Connection { get; init; } = new();

        [JsonPropertyName("datasets")]
        public List<OvertureMfcDataset> Datasets { get; } = new();
    }

    internal sealed class OvertureMfcConnection
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "filesystem";

        [JsonPropertyName("properties")]
        public OvertureMfcConnectionProperties Properties { get; init; } = new();
    }

    internal sealed class OvertureMfcConnectionProperties
    {
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;
    }

    internal sealed class OvertureMfcDataset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("alias")]
        public string Alias { get; init; } = string.Empty;

        [JsonPropertyName("properties")]
        public OvertureMfcDatasetProperties Properties { get; } = new();

        [JsonPropertyName("fields")]
        public IReadOnlyList<OvertureMfcField> Fields { get; init; } = [];

        [JsonPropertyName("geometry")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OvertureMfcGeometry? Geometry { get; init; }
    }

    internal sealed class OvertureMfcDatasetProperties
    {
        [JsonPropertyName("fileformat")]
        public string FileFormat { get; init; } = "parquet";
    }

    internal sealed record OvertureMfcField(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("visible")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Visible = null,
        [property: JsonPropertyName("sourceType")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceType = null);

    internal sealed class OvertureMfcGeometry
    {
        [JsonPropertyName("geometryType")]
        public string GeometryType { get; init; } = "esriGeometryAny";

        [JsonPropertyName("spatialReference")]
        public OvertureMfcSpatialReference SpatialReference { get; init; } = new();

        [JsonPropertyName("fields")]
        public IReadOnlyList<OvertureMfcGeometryField> Fields { get; init; } = [];
    }

    internal sealed class OvertureMfcSpatialReference
    {
        [JsonPropertyName("wkid")]
        public int Wkid { get; init; } = 4326;
    }

    internal sealed class OvertureMfcGeometryField
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = OvertureMfcSchemaPlanner.GeometryColumn;

        [JsonPropertyName("formats")]
        public IReadOnlyList<string> Formats { get; init; } = ["WKB"];
    }

    internal sealed record OvertureDuckDbColumn(string Name, string Type);

    internal sealed record OvertureMfcDatasetInspection(
        IReadOnlyList<OvertureDuckDbColumn> Columns,
        string? GeometryType);
}
