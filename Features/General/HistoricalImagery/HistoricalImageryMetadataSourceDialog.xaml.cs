using System.Collections.Generic;
using System.Linq;
using System.Windows;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Features.General.HistoricalImagery
{
    internal partial class HistoricalImageryMetadataSourceDialog : Window
    {
        internal HistoricalImageryMetadataSourceDialog(IEnumerable<WaybackMetadataSourceCandidate> candidates)
        {
            InitializeComponent();
            var items = candidates.ToList();
            CandidatesListBox.ItemsSource = items;
            if (items.Count > 0)
            {
                CandidatesListBox.SelectedIndex = 0;
            }
        }

        internal WaybackMetadataSourceCandidate? SelectedCandidate
            => CandidatesListBox.SelectedItem as WaybackMetadataSourceCandidate;

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCandidate == null)
            {
                MessageBox.Show("请选择一个查询来源。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
