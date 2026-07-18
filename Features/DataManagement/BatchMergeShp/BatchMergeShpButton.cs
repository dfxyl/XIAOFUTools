using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.BatchMergeShp
{
    /// <summary>
    /// 批量合并 SHP 工具按钮入口。
    /// </summary>
    internal class BatchMergeShpButton : Button
    {
        protected override void OnClick()
        {
            try
            {

                BatchMergeShpDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开工具时出错: {ex.Message}", "错误");
            }
        }
    }
}
