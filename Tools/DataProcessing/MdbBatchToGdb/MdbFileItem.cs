using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal sealed class MdbFileItem : PropertyChangedBase
    {
        private bool _isSelected = true;
        private string _status = "待处理";

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Name { get; set; } = string.Empty;

        public string FullPath { get; set; } = string.Empty;

        public string RelativePath { get; set; } = string.Empty;

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }
    }
}
