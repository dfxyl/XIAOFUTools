using System.Windows;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    /// <summary>
    /// 字段配置对话框
    /// </summary>
    public partial class FieldConfigDialog : Window
    {
        public FieldConfigDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
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
