using System.Windows;

namespace XIAOFUTools.Tools.QuickAddData
{
    public partial class QuickDataTextInputDialog : Window
    {
        public QuickDataTextInputDialog(string title, string prompt, string initialValue = "")
        {
            InitializeComponent();
            Title = title;
            PromptTextBlock.Text = prompt;
            InputTextBox.Text = initialValue ?? string.Empty;
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }

        public string ResponseText => InputTextBox.Text?.Trim();

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ResponseText))
            {
                MessageBox.Show("请输入名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

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
