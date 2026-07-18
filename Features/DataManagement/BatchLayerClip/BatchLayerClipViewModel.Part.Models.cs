using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using System.Globalization;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace XIAOFUTools.Features.DataManagement.BatchLayerClip
{
    internal partial class BatchLayerClipViewModel
    {

        private sealed class GroupPreparationResult
        {
            public string FieldName { get; set; } = string.Empty;
            public FieldType FieldType { get; set; }
            public List<FieldGroupInfo> Groups { get; set; } = new List<FieldGroupInfo>();
        }

        private sealed class FieldGroupInfo
        {
            public FieldGroupKey Key { get; set; }
            public string DisplayValue { get; set; } = string.Empty;
            public string OutputName { get; set; } = string.Empty;
            public int FeatureCount { get; set; }
        }

        private readonly struct FieldGroupKey : IEquatable<FieldGroupKey>
        {
            public FieldGroupKey(object rawValue, FieldType fieldType)
            {
                FieldType = fieldType;

                if (rawValue == null || rawValue == DBNull.Value)
                {
                    IsNull = true;
                    Value = null;
                    return;
                }

                IsNull = false;

                switch (fieldType)
                {
                    case FieldType.Integer:
                    case FieldType.SmallInteger:
                        Value = Convert.ToInt64(rawValue, CultureInfo.InvariantCulture);
                        break;
                    case FieldType.Double:
                    case FieldType.Single:
                        Value = Convert.ToDouble(rawValue, CultureInfo.InvariantCulture);
                        break;
                    default:
                        Value = rawValue.ToString();
                        break;
                }
            }

            public FieldType FieldType { get; }
            public object Value { get; }
            public bool IsNull { get; }

            public bool Equals(FieldGroupKey other)
            {
                if (FieldType != other.FieldType || IsNull != other.IsNull)
                    return false;

                if (IsNull)
                    return true;

                return Value?.Equals(other.Value) ?? other.Value == null;
            }

            public override bool Equals(object obj) => obj is FieldGroupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)FieldType;
                    hash = (hash * 397) ^ (IsNull ? 1 : 0);
                    hash = (hash * 397) ^ (Value?.GetHashCode() ?? 0);
                    return hash;
                }
            }
        }
    }
}
