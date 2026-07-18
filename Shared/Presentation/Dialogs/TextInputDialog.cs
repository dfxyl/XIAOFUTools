using System;
using System.Windows;
using System.Windows.Controls;

namespace XIAOFUTools.Shared.Presentation.Dialogs
{
    /// <summary>
    /// 提供统一的单行文本输入窗口，避免功能 ViewModel 直接依赖具体 WPF 窗口。
    /// </summary>
    internal static class TextInputDialog
    {
        internal static string Prompt(
            string title,
            string prompt,
            string initialValue = "",
            string requiredMessage = "请输入名称。")
        {
            var input = new TextBox
            {
                Height = 28,
                Text = initialValue ?? string.Empty
            };
            input.SetResourceReference(FrameworkElement.StyleProperty, "TextBoxStyle");

            var window = new Window
            {
                Title = title,
                Height = 165,
                Width = 360,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Resources = CreateControlStylesResources()
            };
            if (Application.Current?.MainWindow is Window owner)
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var promptBlock = new TextBlock { Text = prompt ?? string.Empty, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(promptBlock, 0);
            root.Children.Add(promptBlock);
            Grid.SetRow(input, 1);
            root.Children.Add(input);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var cancel = new Button { Content = "取消", Width = 72, Margin = new Thickness(0, 0, 8, 0) };
            cancel.SetResourceReference(FrameworkElement.StyleProperty, "CancelButtonStyle");
            cancel.Click += (_, _) => window.Close();
            var confirm = new Button { Content = "确定", Width = 72 };
            confirm.SetResourceReference(FrameworkElement.StyleProperty, "ExecuteButtonStyle");
            confirm.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(input.Text))
                {
                    PresentationServices.Dialogs.Show(requiredMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    input.Focus();
                    return;
                }

                window.DialogResult = true;
                window.Close();
            };
            actions.Children.Add(cancel);
            actions.Children.Add(confirm);
            Grid.SetRow(actions, 2);
            root.Children.Add(actions);

            window.Content = root;
            window.Loaded += (_, _) =>
            {
                input.SelectAll();
                input.Focus();
            };

            return window.ShowDialog() == true ? input.Text.Trim() : null;
        }

        private static ResourceDictionary CreateControlStylesResources()
        {
            var resources = new ResourceDictionary();
            resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/XIAOFUTools;component/Shared/Presentation/Styles/ControlStyles.xaml",
                    UriKind.Absolute)
            });
            return resources;
        }
    }
}
