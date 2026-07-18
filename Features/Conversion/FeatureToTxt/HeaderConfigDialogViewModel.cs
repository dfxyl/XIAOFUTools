using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    /// <summary>
    /// 头部信息配置对话框ViewModel
    /// </summary>
    internal class HeaderConfigDialogViewModel : PropertyChangedBase
    {
        private ObservableCollection<HeaderConfig> _configs;
        public ObservableCollection<HeaderConfig> Configs
        {
            get => _configs;
            set => SetProperty(ref _configs, value);
        }

        private HeaderConfig _selectedConfig;
        public HeaderConfig SelectedConfig
        {
            get => _selectedConfig;
            set => SetProperty(ref _selectedConfig, value);
        }

        public HeaderConfigDialogViewModel()
        {
            // 加载配置
            var loadedConfigs = HeaderConfigManager.LoadConfigs();
            Configs = new ObservableCollection<HeaderConfig>(loadedConfigs);

            // 选择第一个配置
            if (Configs.Count > 0)
            {
                SelectedConfig = Configs[0];
            }
        }

        private ICommand _addConfigCommand;
        public ICommand AddConfigCommand => _addConfigCommand ?? (_addConfigCommand = new RelayCommand(() =>
        {
            var newConfig = new HeaderConfig
            {
                Name = $"新配置 {Configs.Count + 1}",
                HeaderText = @"[属性描述]
坐标系=2000国家大地坐标系
几度分带=3
投影类型=高斯克吕格
计量单位=米
带号=37
精度=0.001
转换参数=,,,,,,
[地块坐标]"
            };

            Configs.Add(newConfig);
            SelectedConfig = newConfig;
        }));

        private ICommand _deleteConfigCommand;
        public ICommand DeleteConfigCommand => _deleteConfigCommand ?? (_deleteConfigCommand = new RelayCommand(() =>
        {
            if (SelectedConfig == null) return;

            // 不允许删除最后一个配置
            if (Configs.Count <= 1)
            {
                PresentationServices.Dialogs.Show("至少需要保留一个配置！", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var result = PresentationServices.Dialogs.Show(
                $"确定要删除配置 '{SelectedConfig.Name}' 吗？",
                "确认删除",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                var index = Configs.IndexOf(SelectedConfig);
                Configs.Remove(SelectedConfig);

                // 选择下一个配置
                if (Configs.Count > 0)
                {
                    var newIndex = Math.Min(index, Configs.Count - 1);
                    SelectedConfig = Configs[newIndex];
                }
            }
        }));

        /// <summary>
        /// 保存配置
        /// </summary>
        public void SaveConfigs()
        {
            HeaderConfigManager.SaveConfigs(Configs.ToList());
        }
    }
}
