using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.QuickAddData
{
    internal class QuickAddDataButton : Button
    {
        protected override void OnClick()
        {
            try
            {

                QuickAddDataDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开快捷添加数据面板失败: {ex.Message}", "错误");
            }
        }
    }
}
