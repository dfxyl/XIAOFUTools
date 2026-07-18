using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace XIAOFUTools.Features.Analysis.BrowseFeatures
{
    public partial class BrowseFeaturesDockPaneView : UserControl
    {
        private readonly BrowseFeaturesDockPaneViewModel _viewModel;
        private bool _hasInitialized;

        public BrowseFeaturesDockPaneView()
        {
            InitializeComponent();
            _viewModel = new BrowseFeaturesDockPaneViewModel();
            DataContext = _viewModel;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_hasInitialized)
            {
                _hasInitialized = true;
                await _viewModel.RefreshLayersAsync();
            }
            Focus();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // DockPane 在切换界面时也会触发 Unloaded，不能在这里清掉状态与订阅。
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 文本输入或下拉框焦点时，不劫持方向键。
            if (e.OriginalSource is TextBoxBase || e.OriginalSource is ComboBox || e.OriginalSource is ComboBoxItem)
            {
                return;
            }

            if (_viewModel.HandleDirectionKey(e.Key))
            {
                e.Handled = true;
            }
        }
    }
}
