using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DwgFieldOption = XIAOFUTools.Tools.PolygonToDwgWithFill.FieldOption;
using DwgViewModel = XIAOFUTools.Tools.PolygonToDwgWithFill.PolygonToDwgWithFillDockPaneViewModel;

namespace XIAOFUTools.Tools.PolygonToDxfWithFill
{
    public partial class FieldNamingDialog : Window
    {
        private Point _dragStartPoint;

        public FieldNamingDialog()
        {
            InitializeComponent();
        }

        public FieldNamingDialogViewModel VM => DataContext as FieldNamingDialogViewModel;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if (VM.UseFieldNaming)
                rdoField.IsChecked = true;
            else
                rdoUnique.IsChecked = true;
        }

        private void UniqueValueMode_Checked(object sender, RoutedEventArgs e)
        {
            if (VM != null) VM.UseFieldNaming = false;
        }

        private void FieldMode_Checked(object sender, RoutedEventArgs e)
        {
            if (VM != null) VM.UseFieldNaming = true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void FieldsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void FieldsListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (sender is not ListBox lb) return;
            var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (item == null) return;
            var data = lb.ItemContainerGenerator.ItemFromContainer(item);
            if (data == null) return;
            DragDrop.DoDragDrop(item, data, DragDropEffects.Move);
        }

        private void FieldsListBox_Drop(object sender, DragEventArgs e)
        {
            if (VM == null) return;
            if (sender is not ListBox lb) return;
            var data = e.Data.GetData(typeof(FieldOption)) as FieldOption;
            if (data == null) return;

            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            int oldIndex = VM.Fields.IndexOf(data);
            int newIndex = -1;
            if (targetItem != null)
            {
                var targetData = lb.ItemContainerGenerator.ItemFromContainer(targetItem) as FieldOption;
                newIndex = targetData != null ? VM.Fields.IndexOf(targetData) : -1;
            }
            if (oldIndex < 0) return;
            if (newIndex < 0) newIndex = VM.Fields.Count - 1;
            if (newIndex == oldIndex) return;

            VM.Fields.Move(oldIndex, newIndex);
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }

    public partial class FieldNamingDialogViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<FieldOption> Fields { get; set; } = new();

        private bool _useFieldNaming;
        public bool UseFieldNaming
        {
            get => _useFieldNaming;
            set
            {
                if (_useFieldNaming != value)
                {
                    _useFieldNaming = value;
                    OnPropertyChanged(nameof(UseFieldNaming));
                }
            }
        }

        private string _separator = "_";
        public string Separator
        {
            get => _separator;
            set
            {
                if (_separator != value)
                {
                    _separator = value;
                    OnPropertyChanged(nameof(Separator));
                }
            }
        }

        public FieldNamingDialogViewModel() { }

        internal static FieldNamingDialogViewModel FromMainVM(PolygonToDxfWithFillDockPaneViewModel vm)
        {
            var dlgVm = new FieldNamingDialogViewModel
            {
                UseFieldNaming = vm.UseFieldNaming,
                Separator = vm.FieldNamingSeparator
            };

            // 复制字段并保持顺序与别名
            foreach (var f in vm.NamingFields)
            {
                dlgVm.Fields.Add(new FieldOption { Name = f.Name, Alias = f.Alias, IsSelected = f.IsSelected });
            }
            return dlgVm;
        }

        internal void ApplyToMainVM(PolygonToDxfWithFillDockPaneViewModel vm)
        {
            vm.UseFieldNaming = this.UseFieldNaming;
            vm.FieldNamingSeparator = this.Separator;

            // 用对话框顺序覆盖主VM的字段集合，保留别名
            vm.NamingFields.Clear();
            foreach (var f in this.Fields)
            {
                vm.NamingFields.Add(new FieldOption { Name = f.Name, Alias = f.Alias, IsSelected = f.IsSelected });
            }
        }

        internal static FieldNamingDialogViewModel FromMainVM(DwgViewModel vm)
        {
            var dlgVm = new FieldNamingDialogViewModel
            {
                UseFieldNaming = vm.UseFieldNaming,
                Separator = vm.FieldNamingSeparator
            };

            foreach (var f in vm.NamingFields)
            {
                dlgVm.Fields.Add(new FieldOption { Name = f.Name, Alias = f.Alias, IsSelected = f.IsSelected });
            }
            return dlgVm;
        }

        internal void ApplyToMainVM(DwgViewModel vm)
        {
            if (vm == null) return;
            vm.UseFieldNaming = this.UseFieldNaming;
            vm.FieldNamingSeparator = this.Separator;

            vm.NamingFields.Clear();
            foreach (var f in this.Fields)
            {
                vm.NamingFields.Add(new DwgFieldOption { Name = f.Name, Alias = f.Alias, IsSelected = f.IsSelected });
            }
        }
    }

    // INotifyPropertyChanged 实现
    public partial class FieldNamingDialogViewModel
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
