using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace XIAOFUTools.Tools.BatchAddData
{
    /// <summary>
    /// BatchAddDataDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class BatchAddDataDockPaneView : UserControl
    {
        private BatchAddDataViewModel _viewModel;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public BatchAddDataDockPaneView()
        {
            InitializeComponent();
            _viewModel = new BatchAddDataViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// 当控件加载时的处理
        /// </summary>
        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 初始化操作
        }

        /// <summary>
        /// 数据网格选择变化事件
        /// </summary>
        private void DataItemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid == null || _viewModel == null) return;

            // 只处理新增的选择项
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                // 检查是否按住了Ctrl键或者是多选
                bool isMultiSelect = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || dataGrid.SelectedItems.Count > 1;

                if (isMultiSelect)
                {
                    // 多选模式：对新选中的项切换勾选状态
                    foreach (DataItem item in e.AddedItems)
                    {
                        if (item != null)
                        {
                            item.IsSelected = !item.IsSelected;
                        }
                    }
                }
                else
                {
                    // 单选模式：切换单个项的勾选状态
                    foreach (DataItem item in e.AddedItems)
                    {
                        if (item != null)
                        {
                            item.IsSelected = !item.IsSelected;
                            break; // 单选只处理第一个
                        }
                    }
                }
            }
        }
    }
}
