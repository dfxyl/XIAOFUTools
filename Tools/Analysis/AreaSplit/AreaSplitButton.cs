using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework;
using System.Threading.Tasks;

namespace XIAOFUTools.Tools.AreaSplit
{
    /// <summary>
    /// 面积分割按钮类
    /// </summary>
    internal class AreaSplitButton : Button
    {
        protected override async void OnClick()
        {
            // 激活面积分割工具
            await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_AreaSplitTool");
        }
    }
}
