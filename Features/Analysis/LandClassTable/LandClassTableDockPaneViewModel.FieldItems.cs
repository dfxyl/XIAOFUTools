using ArcGIS.Core.Data;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    internal sealed partial class LandClassTableDockPaneViewModel
    {
        private static LandClassFieldItem CreateFieldItem(Field field)
        {
            return new LandClassFieldItem
            {
                FieldName = field.Name,
                Alias = field.AliasName,
                FieldType = GetFieldTypeDisplayName(field.FieldType)
            };
        }
    }
}
