using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.User.Custom.DevZoneCheck
{
    /// <summary>
    /// 开发区整合优化核查按钮
    /// </summary>
    internal class DevZoneCheckButton : Button
    {
        protected override void OnClick()
        {
            DevZoneCheckDockPane.Show();
        }
    }
}
