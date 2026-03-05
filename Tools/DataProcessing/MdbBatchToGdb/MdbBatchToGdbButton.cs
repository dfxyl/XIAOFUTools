using System;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal class MdbBatchToGdbButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("MDB批量转GDB"))
                {
                    return;
                }

                MdbBatchToGdbDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开工具时出错: {ex.Message}", "错误");
            }
        }
    }
}
