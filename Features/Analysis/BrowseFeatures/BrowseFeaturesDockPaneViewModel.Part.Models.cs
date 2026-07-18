using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.User.Settings;
using XIAOFUTools.Features.Analysis.BrowseFeatures.Core;

namespace XIAOFUTools.Features.Analysis.BrowseFeatures
{
    internal sealed partial class BrowseFeaturesDockPaneViewModel
    {
        internal sealed class ScopeModeOption
        {
            public BrowseScopeMode Value { get; init; }
            public string DisplayName { get; init; } = string.Empty;
            public override string ToString() => DisplayName;
        }


        internal sealed class SortModeOption
        {
            public BrowseSortMode Value { get; init; }
            public string DisplayName { get; init; } = string.Empty;
            public override string ToString() => DisplayName;
        }


        internal sealed class SortFieldOption
        {
            public string Name { get; init; } = string.Empty;
            public string Alias { get; init; } = string.Empty;
            public FieldType FieldType { get; init; }
            public string DisplayName => string.IsNullOrWhiteSpace(Alias) || string.Equals(Name, Alias, StringComparison.Ordinal)
                ? Name
                : $"{Name}({Alias})";
            public override string ToString() => DisplayName;
        }


        private sealed class CurrentFeatureContext
        {
            public long ObjectId { get; init; }
            public Geometry Geometry { get; init; }
            public string GeometryType { get; init; } = string.Empty;
            public string GeometryWkt { get; init; } = string.Empty;
            public IReadOnlyList<Envelope> PartEnvelopes { get; init; } = Array.Empty<Envelope>();
        }


        internal sealed class SnapshotListItem : PropertyChangedBase
        {
            public int DisplayIndex { get; init; }
            public long ObjectId { get; init; }

            private string _reviewStatusText = "未判定";
            private string _notesPreview = string.Empty;

            public string ReviewStatusText
            {
                get => _reviewStatusText;
                set
                {
                    if (SetProperty(ref _reviewStatusText, value))
                    {
                        NotifyPropertyChanged(() => InlineSummary);
                    }
                }
            }

            public string NotesPreview
            {
                get => _notesPreview;
                set
                {
                    if (SetProperty(ref _notesPreview, value))
                    {
                        NotifyPropertyChanged(() => InlineSummary);
                    }
                }
            }

            public string DisplayText => $"{DisplayIndex:D5} | OID {ObjectId}";

            public string InlineSummary => string.IsNullOrWhiteSpace(NotesPreview)
                ? $"{DisplayText} | {ReviewStatusText}"
                : $"{DisplayText} | {ReviewStatusText} | {NotesPreview}";
        }

    }
}
