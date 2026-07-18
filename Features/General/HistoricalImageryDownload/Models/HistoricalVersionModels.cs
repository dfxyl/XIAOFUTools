#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload
{
    public enum HistoricalImageryProviderType
    {
        GoogleEarth = 0,
        Wayback = 1
    }

    public sealed class HistoricalVersionItem
    {
        public HistoricalImageryProviderType Provider { get; set; }

        public string VersionId { get; set; } = string.Empty;

        public string DisplayDate { get; set; } = string.Empty;

        public string? Summary { get; set; }

        public string? MetadataLayerUrl { get; set; }

        public string? TileUrlTemplate { get; set; }

        public string? AcquisitionDate { get; set; }

        public string? ChangeKey { get; set; }

        public bool HasFullCoverage { get; set; } = true;

        public string DisplayText
        {
            get
            {
                var providerText = Provider == HistoricalImageryProviderType.GoogleEarth ? "Google" : "Wayback";
                var coverageText = HasFullCoverage ? "full" : "partial";
                var dateText = string.IsNullOrWhiteSpace(AcquisitionDate)
                    ? DisplayDate
                    : $"{DisplayDate} / {AcquisitionDate}";
                return string.IsNullOrWhiteSpace(Summary)
                    ? $"{providerText} {dateText} ({coverageText})"
                    : $"{providerText} {dateText} - {Summary} ({coverageText})";
            }
        }

        public override string ToString() => DisplayText;
    }

    public sealed class HistoricalVersionSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public HistoricalVersionSelectionItem(HistoricalVersionItem version)
        {
            Version = version;
        }

        public HistoricalVersionItem Version { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public string DisplayText => Version.DisplayText;

        public override string ToString() => DisplayText;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
