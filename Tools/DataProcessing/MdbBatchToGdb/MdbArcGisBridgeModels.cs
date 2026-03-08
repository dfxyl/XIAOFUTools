using System;
using System.Collections.Generic;
using ArcFieldType = ArcGIS.Core.Data.FieldType;
using ArcGeometryType = ArcGIS.Core.Geometry.GeometryType;
using GdalFieldSubType = OSGeo.OGR.FieldSubType;
using GdalFieldType = OSGeo.OGR.FieldType;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal sealed class MdbLayerSchema
    {
        public SourceLayerInfo LayerInfo { get; init; }

        public bool IsTable { get; init; }

        public ArcGeometryType? GeometryType { get; init; }

        public bool HasZ { get; init; }

        public bool HasM { get; init; }

        public string SpatialReferenceWkt { get; init; } = string.Empty;

        public IReadOnlyList<MdbFieldSchema> Fields { get; init; } = Array.Empty<MdbFieldSchema>();
    }

    internal sealed class MdbFieldSchema
    {
        public int SourceIndex { get; init; }

        public string Name { get; init; } = string.Empty;

        public string AliasName { get; init; } = string.Empty;

        public GdalFieldType SourceFieldType { get; init; }

        public GdalFieldSubType SourceFieldSubType { get; init; }

        public ArcFieldType TargetFieldType { get; init; }

        public int? Length { get; init; }

        public int? Precision { get; init; }

        public int? Scale { get; init; }

        public bool IsNullable { get; init; }

        public bool SerializeAsText { get; init; }
    }

    internal sealed class MdbRowData
    {
        public byte[] GeometryWkb { get; init; }

        public object[] Values { get; init; } = Array.Empty<object>();
    }
}
