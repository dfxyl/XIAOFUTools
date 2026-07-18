using ArcGIS.Desktop.Mapping.Controls;
using ArcGIS.Core.Geometry;
using System.Windows;

namespace XIAOFUTools.Shared
{
    /// <summary>
    /// 通用坐标系选择器
    /// </summary>
    public partial class CoordinateSystemSelector : global::ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        /// <summary>
        /// 选择的空间参考系
        /// </summary>
        public SpatialReference SelectedSpatialReference { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public CoordinateSystemSelector()
        {
            InitializeComponent();
            SelectedSpatialReference = null;
            this.SizeChanged += (sender, e) =>
            {
                // 确保窗口不改变大小
                this.Width = 500;  // 固定宽度
                this.Height = 400; // 固定高度
            };
        }

        /// <summary>
        /// 确认选择坐标系
        /// </summary>
        private void btnSelectCoordinateSystem_Click(object sender, RoutedEventArgs e)
        {
            SelectedSpatialReference = coordinateSystemsControl.SelectedSpatialReference;

            if (SelectedSpatialReference == null)
            {
                MessageBox.Show("请先选择一个坐标系。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 检查是否通过 ShowDialog 打开
            if (this.IsLoaded && this.Owner != null)
            {
                DialogResult = true; // 设置对话框结果
            }
            Close(); // 关闭窗口
        }

        /// <summary>
        /// 取消选择
        /// </summary>
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否通过 ShowDialog 打开
            if (this.IsLoaded && this.Owner != null)
            {
                DialogResult = false; // 设置对话框结果
            }
            Close(); // 关闭窗口
        }

        /// <summary>
        /// 显示坐标系选择器并返回选择的坐标系
        /// </summary>
        /// <param name="owner">父窗口</param>
        /// <returns>选择的坐标系，如果取消则返回null</returns>
        public static SpatialReference ShowCoordinateSystemDialog(Window owner = null)
        {
            var selector = new CoordinateSystemSelector();
            if (owner != null)
            {
                selector.Owner = owner;
            }

            var result = selector.ShowDialog();
            return result == true ? selector.SelectedSpatialReference : null;
        }
    }
}
