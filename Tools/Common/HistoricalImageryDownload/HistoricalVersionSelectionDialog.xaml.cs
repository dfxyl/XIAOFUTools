using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace XIAOFUTools.Tools.HistoricalImageryDownload
{
    public partial class HistoricalVersionSelectionDialog : Window, INotifyPropertyChanged
    {
        private readonly IReadOnlyList<bool> _originalSelectionStates;
        private string _selectionSummary = string.Empty;

        public HistoricalVersionSelectionDialog(IList<HistoricalVersionSelectionItem> versions)
        {
            InitializeComponent();
            DataContext = this;

            Versions = new ObservableCollection<HistoricalVersionSelectionItem>(versions);
            _originalSelectionStates = versions.Select(item => item.IsSelected).ToArray();

            foreach (var version in Versions)
            {
                version.PropertyChanged += Version_PropertyChanged;
            }

            UpdateSelectionSummary();
        }

        public ObservableCollection<HistoricalVersionSelectionItem> Versions { get; }

        public string SelectionSummary
        {
            get => _selectionSummary;
            set
            {
                if (_selectionSummary == value)
                {
                    return;
                }

                _selectionSummary = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionSummary)));
            }
        }

        private void Version_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HistoricalVersionSelectionItem.IsSelected))
            {
                UpdateSelectionSummary();
            }
        }

        private void UpdateSelectionSummary()
        {
            var selectedCount = Versions.Count(item => item.IsSelected);
            SelectionSummary = $"已勾选 {selectedCount} 个时间，共 {Versions.Count} 个。";
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var version in Versions)
            {
                version.IsSelected = true;
            }
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var version in Versions)
            {
                version.IsSelected = false;
            }
        }

        private void InvertSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var version in Versions)
            {
                version.IsSelected = !version.IsSelected;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            for (var index = 0; index < Versions.Count && index < _originalSelectionStates.Count; index++)
            {
                Versions[index].IsSelected = _originalSelectionStates[index];
            }

            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
