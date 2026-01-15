using System.Windows;

namespace XIAOFUTools.Tools.FeatureToTxt
{
    /// <summary>
    /// 头部信息配置对话框
    /// </summary>
    public partial class HeaderConfigDialog : Window
    {
        public HeaderConfigDialog()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
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
