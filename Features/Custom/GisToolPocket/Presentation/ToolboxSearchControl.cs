#nullable enable

using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

using XIAOFUTools.Features.Custom.GisToolPocket.Application;
using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Presentation
{
    internal sealed class ToolboxSearchControl : CustomControl
    {
        protected override FrameworkElement OnCreateContent()
        {
            return new ToolboxSearchBox();
        }

        protected override void OnUpdate()
        {
            Enabled = true;
        }
    }

    internal sealed class ToolboxSearchBox : Grid
    {
        private const int ResultLimit = 12;
        private static readonly Brush NormalBorderBrush = new SolidColorBrush(Color.FromRgb(94, 110, 130));
        private static readonly Brush HoverBackgroundBrush = new SolidColorBrush(Color.FromRgb(248, 251, 255));
        private static readonly Brush ButtonBackgroundBrush = new SolidColorBrush(Color.FromRgb(246, 248, 251));
        private static readonly Brush ButtonHoverBrush = new SolidColorBrush(Color.FromRgb(232, 241, 252));
        private static readonly Brush ButtonBorderBrush = new SolidColorBrush(Color.FromRgb(150, 160, 172));
        private static readonly Brush ButtonHoverBorderBrush = new SolidColorBrush(Color.FromRgb(70, 130, 190));

        private readonly Border _manageButton;
        private readonly Border _donateButton;
        private readonly ArcGIS.Desktop.Framework.Controls.SearchTextBox _searchBox;
        private readonly Popup _popup;
        private readonly ListBox _resultList;
        private readonly Dictionary<ListBoxItem, ToolboxSearchEntry> _entryByItem = new();

        public ToolboxSearchBox()
        {
            Width = 118;
            MinWidth = 104;
            Height = 52;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

            var manageContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            manageContent.Children.Add(new Image
            {
                Source = ToolboxIconService.SmallImageSource("Settings"),
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 4, 0)
            });
            manageContent.Children.Add(new TextBlock
            {
                Text = "管理",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = SystemColors.ControlTextBrush
            });

            _manageButton = new Border
            {
                Height = 23,
                Margin = new Thickness(1, 0, 1, 3),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = ButtonBorderBrush,
                Background = ButtonBackgroundBrush,
                Cursor = Cursors.Hand,
                Child = manageContent
            };
            Grid.SetColumn(_manageButton, 0);
            _manageButton.MouseEnter += (_, _) =>
            {
                _manageButton.Background = ButtonHoverBrush;
                _manageButton.BorderBrush = ButtonHoverBorderBrush;
            };
            _manageButton.MouseLeave += (_, _) =>
            {
                _manageButton.Background = ButtonBackgroundBrush;
                _manageButton.BorderBrush = ButtonBorderBrush;
            };
            _manageButton.MouseLeftButtonUp += (_, _) => OpenManagerWindow();

            _donateButton = new Border
            {
                Width = 23,
                Height = 23,
                Margin = new Thickness(0, 0, 1, 3),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = ButtonBorderBrush,
                Background = ButtonBackgroundBrush,
                Cursor = Cursors.Hand,
                ToolTip = "赞赏支持",
                Child = CreateThumbIcon()
            };
            Grid.SetColumn(_donateButton, 2);
            _donateButton.MouseEnter += (_, _) =>
            {
                _donateButton.Background = ButtonHoverBrush;
                _donateButton.BorderBrush = ButtonHoverBorderBrush;
            };
            _donateButton.MouseLeave += (_, _) =>
            {
                _donateButton.Background = ButtonBackgroundBrush;
                _donateButton.BorderBrush = ButtonBorderBrush;
            };
            _donateButton.MouseLeftButtonUp += (_, _) => OpenDonationWindow();

            _searchBox = new ArcGIS.Desktop.Framework.Controls.SearchTextBox
            {
                Height = 22,
                Margin = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                InfoText = "搜索",
                IsIconEnabled = true,
                ShowMagnifier = true,
                ShowHistory = false,
                ShowFolderSuggestions = false,
                ShowGroup = false,
                IsPopupEnabled = false,
                CornerRadius = new CornerRadius(4),
                SearchEventTimeDelay = new Duration(TimeSpan.FromMilliseconds(120)),
                Padding = new Thickness(4, 0, 4, 0)
            };
            Grid.SetRow(_searchBox, 1);

            _resultList = new ListBox
            {
                MinWidth = 320,
                MaxHeight = 320,
                BorderThickness = new Thickness(1),
                BorderBrush = NormalBorderBrush,
                Background = SystemColors.WindowBrush,
                Foreground = SystemColors.ControlTextBrush
            };

            _popup = new Popup
            {
                PlacementTarget = _searchBox,
                Placement = PlacementMode.Bottom,
                AllowsTransparency = true,
                StaysOpen = false,
                Child = _resultList
            };

            ToolTip = "搜索已添加到 GIS 工具口袋的工具";
            topRow.Children.Add(_manageButton);
            topRow.Children.Add(_donateButton);
            Children.Add(topRow);
            Children.Add(_searchBox);

            _searchBox.TextChanged += OnTextChanged;
            _searchBox.GotKeyboardFocus += OnGotKeyboardFocus;
            _searchBox.PreviewKeyDown += OnPreviewKeyDown;
            _resultList.PreviewMouseLeftButtonUp += OnResultMouseLeftButtonUp;
            _resultList.PreviewKeyDown += OnResultPreviewKeyDown;

            RefreshResults(openPopup: false);
        }

        private static void OpenManagerWindow()
        {
            try
            {
                ShowModalWindow(new ToolboxManagerWindow());
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开管理窗口失败：{ex.Message}", "GIS 工具口袋");
            }
        }

        private static void OpenDonationWindow()
        {
            try
            {
                ShowModalWindow(new ToolboxDonationWindow());
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开赞赏窗口失败：{ex.Message}", "GIS 工具口袋");
            }
        }

        private static void ShowModalWindow(Window window)
        {
            var owner = System.Windows.Application.Current?.MainWindow;
            if (owner is not null && owner.IsVisible)
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else if (window.WindowStartupLocation == WindowStartupLocation.CenterOwner)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            window.ShowDialog();
        }

        private static FrameworkElement CreateThumbIcon()
        {
            var canvas = new Canvas
            {
                Width = 16,
                Height = 16
            };

            var geometry = Geometry.Parse("M6,13 L11.5,13 C12.4,13 13,12.35 13.2,11.55 L14,7.55 C14.2,6.55 13.45,5.6 12.4,5.6 L9.2,5.6 L9.7,3.3 C9.9,2.2 9.2,1.2 8.15,1.2 L7.55,1.2 L5.1,5.8 L3.2,5.8 L3.2,13 Z M2,5.8 L4,5.8 L4,13 L2,13 Z");
            var path = new System.Windows.Shapes.Path
            {
                Data = geometry,
                Fill = new SolidColorBrush(Color.FromRgb(194, 139, 38)),
                Stroke = new SolidColorBrush(Color.FromRgb(126, 88, 20)),
                StrokeThickness = 0.8,
                Stretch = Stretch.Fill,
                Width = 16,
                Height = 16
            };
            canvas.Children.Add(path);
            return canvas;
        }

        private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            RefreshResults(openPopup: true);
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshResults(openPopup: true);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OpenSelectedOrBestMatch();
                return;
            }

            if (e.Key == Key.Down && _resultList.Items.Count > 0)
            {
                e.Handled = true;
                if (!_popup.IsOpen)
                    _popup.IsOpen = true;

                _resultList.SelectedIndex = Math.Max(0, _resultList.SelectedIndex);
                if (_resultList.ItemContainerGenerator.ContainerFromIndex(_resultList.SelectedIndex) is ListBoxItem item)
                    item.Focus();
                return;
            }

            if (e.Key == Key.Escape)
            {
                _popup.IsOpen = false;
                e.Handled = true;
            }
        }

        private void OnResultPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OpenSelectedResult();
                return;
            }

            if (e.Key == Key.Escape)
            {
                _popup.IsOpen = false;
                _searchBox.Focus();
                e.Handled = true;
            }
        }

        private void OnResultMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedResult();
        }

        private void RefreshResults(bool openPopup)
        {
            var query = _searchBox.Text;
            var entries = ToolboxSearchService.Search(query, ResultLimit);
            _entryByItem.Clear();
            _resultList.Items.Clear();

            if (entries.Count == 0)
            {
                _resultList.Items.Add(CreateMessageItem(string.IsNullOrWhiteSpace(query)
                    ? "没有可搜索的工具"
                    : "没有匹配工具"));
            }
            else
            {
                foreach (var entry in entries)
                {
                    var item = CreateResultItem(entry);
                    _entryByItem[item] = entry;
                    _resultList.Items.Add(item);
                }
            }

            if (openPopup)
                _popup.IsOpen = true;
        }

        private static ListBoxItem CreateMessageItem(string message)
        {
            return new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = message,
                    Margin = new Thickness(8, 6, 8, 6),
                    Foreground = SystemColors.GrayTextBrush
                },
                IsHitTestVisible = false,
                Focusable = false
            };
        }

        private static ListBoxItem CreateResultItem(ToolboxSearchEntry entry)
        {
            var title = ResolveToolTitle(entry.Tool);
            var location = string.IsNullOrWhiteSpace(entry.ToolsetPath)
                ? entry.CatalogTitle
                : $"{entry.CatalogTitle} / {entry.ToolsetPath}";

            var panel = new StackPanel
            {
                Margin = new Thickness(8, 5, 8, 5)
            };
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            panel.Children.Add(new TextBlock
            {
                Text = location,
                FontSize = 11,
                Foreground = SystemColors.GrayTextBrush,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            return new ListBoxItem
            {
                Content = panel,
                ToolTip = entry.Tooltip,
                MinWidth = 320
            };
        }

        private async void OpenSelectedOrBestMatch()
        {
            var selectedEntry = SelectedEntry();
            var entry = selectedEntry ?? ToolboxSearchService.Search(_searchBox.Text, 1).FirstOrDefault();
            if (entry is null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("未找到匹配的工具。", "GIS 工具口袋");
                return;
            }

            await OpenEntryAsync(entry);
        }

        private async void OpenSelectedResult()
        {
            var entry = SelectedEntry();
            if (entry is null)
                return;

            await OpenEntryAsync(entry);
        }

        private ToolboxSearchEntry? SelectedEntry()
        {
            return _resultList.SelectedItem is ListBoxItem item && _entryByItem.TryGetValue(item, out var entry)
                ? entry
                : null;
        }

        private async System.Threading.Tasks.Task OpenEntryAsync(ToolboxSearchEntry entry)
        {
            try
            {
                _popup.IsOpen = false;
                await ToolboxMenuSlotService.OpenToolAsync(entry.Tool);
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开工具失败：{ex.Message}", "GIS 工具口袋");
            }
        }

        private static string ResolveToolTitle(ToolboxTool tool)
        {
            return string.IsNullOrWhiteSpace(tool.Caption) ? tool.Name : tool.Caption;
        }
    }
}
