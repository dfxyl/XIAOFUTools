using System.Collections.Generic;

namespace XIAOFUTools.Features.DataManagement.ExportDatabaseSchema.Core
{
    internal sealed class FeatureDatasetInfo
    {
        public int Index { get; set; }
        public string DatasetName { get; set; } = string.Empty;
        public string DatasetAlias { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    internal sealed class FeatureClassInfo
    {
        public string Name { get; set; } = string.Empty;
        public string AliasName { get; set; } = string.Empty;
        public string GeometryType { get; set; } = string.Empty;
        public string FeatureDataset { get; set; } = string.Empty;
        public List<FieldInfo> Fields { get; } = new();
    }

    internal sealed class FieldInfo
    {
        public string FieldName { get; set; } = string.Empty;
        public string AliasName { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public int? Length { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public bool IsNullable { get; set; }
        public string DefaultValue { get; set; } = string.Empty;
    }
}
