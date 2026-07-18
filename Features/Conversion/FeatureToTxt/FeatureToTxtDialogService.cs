using System;
using System.Windows;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    /// <summary>
    /// 封装要素转文本配置窗口的生命周期，避免主 ViewModel 直接依赖 WPF 窗口与 Owner。
    /// </summary>
    internal interface IFeatureToTxtDialogService
    {
        bool ShowHeaderConfiguration();
        bool ShowFieldConfiguration(FeatureToTxtDockPaneViewModel viewModel);
    }

    internal sealed class FeatureToTxtDialogService : IFeatureToTxtDialogService
    {
        public bool ShowHeaderConfiguration()
        {
            var viewModel = new HeaderConfigDialogViewModel();
            var dialog = new HeaderConfigDialog
            {
                DataContext = viewModel,
                Owner = Application.Current?.MainWindow
            };
            if (dialog.ShowDialog() != true)
            {
                return false;
            }

            viewModel.SaveConfigs();
            return true;
        }

        public bool ShowFieldConfiguration(FeatureToTxtDockPaneViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            var dialog = new FieldConfigDialog
            {
                DataContext = viewModel,
                Owner = Application.Current?.MainWindow
            };
            return dialog.ShowDialog() == true;
        }
    }
}
