using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using System;

namespace XIAOFUTools.Tools.LayoutTools.LayoutTextReplace
{
    /// <summary>
    /// 布局元素查找替换按钮类
    /// </summary>
    internal class LayoutTextReplaceButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开布局元素查找替换停靠窗格
                LayoutTextReplaceDockPane.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
