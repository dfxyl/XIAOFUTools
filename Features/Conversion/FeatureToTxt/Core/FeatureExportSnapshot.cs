using System;
using System.Collections.Generic;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt.Core
{
    internal sealed class FeatureExportSnapshot
    {
        private readonly IReadOnlyDictionary<string, string> _fieldValues;

        internal FeatureExportSnapshot(
            long objectId,
            double area,
            IReadOnlyList<FeatureCoordinateRing> rings,
            IReadOnlyDictionary<string, string> fieldValues)
        {
            ObjectId = objectId;
            Area = area;
            Rings = rings ?? throw new ArgumentNullException(nameof(rings));
            _fieldValues = fieldValues ?? throw new ArgumentNullException(nameof(fieldValues));
        }

        internal long ObjectId { get; }

        internal double Area { get; }

        internal IReadOnlyList<FeatureCoordinateRing> Rings { get; }

        internal string GetFieldValue(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return string.Empty;
            }

            return _fieldValues.TryGetValue(fieldName, out var value)
                ? value ?? string.Empty
                : string.Empty;
        }

        internal string GetMappedValue(string mappingKey)
        {
            if (!FeatureToTxtFieldCatalog.Mappings.TryGetValue(mappingKey, out var fieldNames))
            {
                return string.Empty;
            }

            foreach (var fieldName in fieldNames)
            {
                var value = GetFieldValue(fieldName);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }

    internal sealed record FeatureExportReadResult(
        int SourceCount,
        IReadOnlyList<FeatureExportSnapshot> Snapshots,
        IReadOnlyList<string> Warnings);
}
