using System.Windows;
using System.Windows.Controls;

namespace XIAOFUTools.Tools.PolygonToDxfWithFill
{
    /// <summary>
    /// 面要素图层转DXF[带填充] DockPane视图
    /// </summary>
    public partial class PolygonToDxfWithFillDockPaneView : UserControl
    {
        private PolygonToDxfWithFillDockPaneViewModel _viewModel;

        public PolygonToDxfWithFillDockPaneView()
        {
            InitializeComponent();
            _viewModel = new PolygonToDxfWithFillDockPaneViewModel();
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
            if (res == true)
            {
                if (dlg.VM != null)
                {
                    dlg.VM.ApplyToMainVM(_viewModel);
                    // 刷新命令可用状态
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }
    }
}
