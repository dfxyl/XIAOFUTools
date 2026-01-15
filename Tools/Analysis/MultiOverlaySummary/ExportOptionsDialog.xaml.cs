using System.Windows;

namespace XIAOFUTools.Tools.MultiOverlaySummary
{
    public partial class ExportOptionsDialog : Window
    {
        public bool ExportExcel { get; private set; }
        public bool ExportGdb { get; private set; }

        public ExportOptionsDialog()
        {
            InitializeComponent();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (!ExportExcelCheckBox.IsChecked.GetValueOrDefault() && !ExportGdbCheckBox.IsChecked.GetValueOrDefault())
            {
                MessageBox.Show("请至少选择一项导出内容", "提示");
                return;
            }

            ExportExcel = ExportExcelCheckBox.IsChecked.GetValueOrDefault();
            ExportGdb = ExportGdbCheckBox.IsChecked.GetValueOrDefault();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
