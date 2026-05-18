using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.OverlapCheck
{
    /// <summary>
    /// 图形重叠检查工具按钮
    /// </summary>
    internal class OverlapCheckButton : Button
    {
        protected override void OnClick()
        {
            // 打开停靠窗格
            OverlapCheckDockPane.Show();
        }
    }
}
