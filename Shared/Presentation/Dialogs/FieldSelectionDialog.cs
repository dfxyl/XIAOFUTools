using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ArcGIS.Core.Data;

namespace XIAOFUTools.Shared.Presentation.Dialogs
{
    /// <summary>
    /// 提供跨功能复用的字段多选窗口，不让 ViewModel 直接依赖 WPF 窗口。
    /// </summary>
    internal static class FieldSelectionDialog
    {
        internal static IReadOnlyList<string> Select(
            IEnumerable<Field> fields,
            IEnumerable<string> preselectedFields = null)
        {
            ArgumentNullException.ThrowIfNull(fields);
            var selected = (preselectedFields ?? Array.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var checkBoxes = fields
                .Where(field => field.FieldType is not FieldType.Geometry and not FieldType.OID)
                .Select(field => new CheckBox
                {
                    Content = BuildDisplayName(field),
                    Tag = field.Name,
                    IsChecked = selected.Contains(field.Name),
                    Margin = new Thickness(3),
                    Padding = new Thickness(5, 2, 5, 2)
                })
                .ToList();

            var fieldPanel = new StackPanel();
            foreach (CheckBox checkBox in checkBoxes)
            {
                fieldPanel.Children.Add(checkBox);
            }
            var window = new Window
            {
                Title = "选择保留字段",
                Width = 350,
                Height = 400,
                MinWidth = 300,
                MinHeight = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize,
                Resources = CreateControlStylesResources(),
                Content = BuildContent(fieldPanel, checkBoxes, out var accepted)
            };
            if (Application.Current?.MainWindow is Window owner)
            {
                window.Owner = owner;
            }

            _ = window.ShowDialog();
            return accepted.Value
                ? checkBoxes.Where(checkBox => checkBox.IsChecked == true).Select(checkBox => (string)checkBox.Tag).ToArray()
                : null;
        }

        private static UIElement BuildContent(StackPanel fieldPanel, IReadOnlyList<CheckBox> checkBoxes, out StrongBox<bool> accepted)
        {
            var selectionAccepted = new StrongBox<bool>();
            accepted = selectionAccepted;
            var root = new DockPanel { Margin = new Thickness(12) };
            var selectionSummary = new TextBlock { Margin = new Thickness(0, 0, 0, 10) };
            void UpdateSelectionSummary()
            {
                var selectedCount = checkBoxes.Count(checkBox => checkBox.IsChecked == true);
                selectionSummary.Text = $"已选择 {selectedCount} 个字段，共 {checkBoxes.Count} 个字段";
            }

            foreach (CheckBox checkBox in checkBoxes)
            {
                checkBox.Checked += (_, _) => UpdateSelectionSummary();
                checkBox.Unchecked += (_, _) => UpdateSelectionSummary();
            }

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var confirm = new Button { Content = "确定", Width = 80, Margin = new Thickness(0, 8, 10, 0) };
            confirm.SetResourceReference(FrameworkElement.StyleProperty, "ExecuteButtonStyle");
            var cancel = new Button { Content = "取消", Width = 80, Margin = new Thickness(0, 8, 0, 0) };
            cancel.SetResourceReference(FrameworkElement.StyleProperty, "CancelButtonStyle");
            confirm.Click += (_, _) =>
            {
                selectionAccepted.Value = true;
                Window.GetWindow(confirm)?.Close();
            };
            cancel.Click += (_, _) => Window.GetWindow(cancel)?.Close();
            buttons.Children.Add(confirm);
            buttons.Children.Add(cancel);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            AddAction(actions, "全选", () => SetSelection(checkBoxes, true));
            AddAction(actions, "全不选", () => SetSelection(checkBoxes, false));
            AddAction(actions, "反选", () => checkBoxes.ToList().ForEach(checkBox => checkBox.IsChecked = checkBox.IsChecked != true));
            var header = new TextBlock
            {
                Text = "选择要保留到输出图层的原始字段：",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            DockPanel.SetDock(actions, Dock.Top);
            root.Children.Add(actions);
            DockPanel.SetDock(selectionSummary, Dock.Bottom);
            root.Children.Add(selectionSummary);
            root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = fieldPanel });
            UpdateSelectionSummary();
            return root;
        }

        private static void AddAction(Panel panel, string text, Action action)
        {
            var button = new Button { Content = text, Width = 60, Margin = new Thickness(0, 0, 5, 0) };
            button.SetResourceReference(FrameworkElement.StyleProperty, "DefaultButtonStyle");
            button.Click += (_, _) => action();
            panel.Children.Add(button);
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

        private static void SetSelection(IEnumerable<CheckBox> checkBoxes, bool value)
        {
            foreach (CheckBox checkBox in checkBoxes)
            {
                checkBox.IsChecked = value;
            }
        }

        private static string BuildDisplayName(Field field)
        {
            var alias = string.IsNullOrWhiteSpace(field.AliasName) || field.AliasName == field.Name ? string.Empty : $"（{field.AliasName}）";
            return $"{field.Name}{alias}（{field.FieldType}）";
        }

        private sealed class StrongBox<T>
        {
            internal T Value;
        }
    }
}
