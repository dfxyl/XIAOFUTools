using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Editing.GapCheck
{
    /// <summary>
    /// 缝隙检查工具按钮
    /// </summary>
    internal class GapCheckButton : Button
    {
        protected override void OnClick()
        {
            // 打开停靠窗格
            GapCheckDockPane.Show();
        }
    }
}