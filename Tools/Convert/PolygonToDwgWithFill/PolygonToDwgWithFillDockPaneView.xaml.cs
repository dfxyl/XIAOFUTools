using System.Windows;
using System.Windows.Controls;
using XIAOFUTools.Tools.PolygonToDxfWithFill;

namespace XIAOFUTools.Tools.PolygonToDwgWithFill
{
    /// <summary>
    /// 面要素图层转DWG[带色块填充] DockPane视图
    /// </summary>
    public partial class PolygonToDwgWithFillDockPaneView : UserControl
    {
        private PolygonToDwgWithFillDockPaneViewModel _viewModel;

        public PolygonToDwgWithFillDockPaneView()
        {
            InitializeComponent();
            _viewModel = new PolygonToDwgWithFillDockPaneViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel?.RefreshLayers();
        }

        private void OpenNamingDialog_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            var dlg = new FieldNamingDialog
            {
                Owner = Window.GetWindow(this),
                DataContext = FieldNamingDialogViewModel.FromMainVM(_viewModel)
            };
            var res = dlg.ShowDialog();
            if (res == true && dlg.VM != null)
            {
                dlg.VM.ApplyToMainVM(_viewModel);
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
