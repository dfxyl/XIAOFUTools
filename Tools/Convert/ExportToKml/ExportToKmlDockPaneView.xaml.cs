using System.Windows.Controls;
using System.Windows.Input;

namespace XIAOFUTools.Tools.ExportToKml
{
    /// <summary>
    /// ExportToKmlDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class ExportToKmlDockPaneView : UserControl
    {
        public ExportToKmlDockPaneView()
        {
            InitializeComponent();
            DataContext = new ExportToKmlViewModel();
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ExportToKmlViewModel viewModel)
            {
                viewModel.RefreshLayers();
            }
        }

        private void LabelFieldComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ComboBox comboBox || comboBox.IsDropDownOpen)
            {
                return;
            }

            comboBox.Focus();
            comboBox.IsDropDownOpen = true;
            e.Handled = true;
        }
    }
}
