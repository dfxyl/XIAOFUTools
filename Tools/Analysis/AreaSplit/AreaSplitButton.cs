using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework;
using System.Threading.Tasks;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.AreaSplit
{
    /// <summary>
    /// 面积分割按钮类
    /// </summary>
    internal class AreaSplitButton : Button
    {
        protected override async void OnClick()
        {
            // 检查授权
            if (!AuthorizationChecker.CheckAuthorizationWithPrompt("面积分割工具"))
            {
                return; // 授权无效时，退出操作
            }

            // 激活面积分割工具
            await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_AreaSplitTool");
        }
    }
}
