using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace XIAOFUTools.Tools.ExportToKml
{
    internal static class ExportToKmlFieldSelection
    {
        public static string? ResolveSelectedLabelField(
            string? previousSelectedLabelField,
            IEnumerable<string> availableLabelFields)
        {
            return ResolveSelectedField(previousSelectedLabelField, availableLabelFields, null);
        }

        public static string? ResolveSelectedGroupField(
            string? previousSelectedGroupField,
            IEnumerable<string> availableGroupFields,
            string? defaultGroupField)
        {
            return ResolveSelectedField(previousSelectedGroupField, availableGroupFields, defaultGroupField);
        }

        private static string? ResolveSelectedField(
            string? previousSelectedField,
            IEnumerable<string> availableFields,
            string? defaultField)
        {
            var fields = availableFields?.Where(field => !string.IsNullOrWhiteSpace(field)).ToList()
                ?? new List<string>();

            if (!string.IsNullOrWhiteSpace(previousSelectedField))
            {
                var existingField = fields.FirstOrDefault(field =>
                    string.Equals(field, previousSelectedField, StringComparison.OrdinalIgnoreCase));
                if (existingField != null)
                {
                    return existingField;
                }
            }

            if (!string.IsNullOrWhiteSpace(defaultField))
            {
                var existingDefault = fields.FirstOrDefault(field =>
                    string.Equals(field, defaultField, StringComparison.OrdinalIgnoreCase));
                if (existingDefault != null)
                {
                    return existingDefault;
                }
            }

            return fields.FirstOrDefault();
        }
    }
}
