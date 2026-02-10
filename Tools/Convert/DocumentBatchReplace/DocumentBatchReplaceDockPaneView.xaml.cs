using System.Windows;
using System.Windows.Controls;

namespace XIAOFUTools.Tools.DocumentBatchReplace
{
    public partial class DocumentBatchReplaceDockPaneView : UserControl
    {
        public DocumentBatchReplaceDockPaneView()
        {
            InitializeComponent();
            DataContext = new DocumentBatchReplaceDockPaneViewModel();
        }

        private void DropArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void DropArea_Drop(object sender, DragEventArgs e)
        {
            if (DataContext is not DocumentBatchReplaceDockPaneViewModel viewModel)
            {
                return;
            }

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            {
                viewModel.AddInputPaths(paths);
            }
        }
    }
}
