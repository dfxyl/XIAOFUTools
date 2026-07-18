using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.MdbBatchToGdb
{
    internal class MdbBatchToGdbButton : Button
    {
        protected override void OnClick()
        {
            try
            {

                MdbBatchToGdbDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开工具时出错: {ex.Message}", "错误");
            }
        }
    }
}
