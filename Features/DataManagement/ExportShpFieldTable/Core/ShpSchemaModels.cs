using System.Collections.Generic;

namespace XIAOFUTools.Features.DataManagement.ExportShpFieldTable.Core
{
    internal sealed class ShpLayerSchemaInfo
    {
        public string Name { get; set; } = string.Empty;
        public string AliasName { get; set; } = string.Empty;
        public string GeometryType { get; set; } = string.Empty;
        public List<ShpFieldSchemaInfo> Fields { get; } = new();
    }

    internal sealed class ShpFieldSchemaInfo
    {
        public string FieldName { get; set; } = string.Empty;
        public string AliasName { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public int? Length { get; set; }
        public int? Scale { get; set; }
    }
}
