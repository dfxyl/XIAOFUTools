using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.WordToPdf
{
    internal class WordToPdfButton : Button
    {
        protected override void OnClick()
        {
            try
            {

                WordToPdfDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
