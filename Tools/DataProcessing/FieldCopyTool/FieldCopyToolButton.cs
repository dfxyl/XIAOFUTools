using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.FieldCopyTool
{
    internal class FieldCopyToolButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("字段复制工具"))
                {
                    return;
                }

                // 打开字段复制工具对话框
                FieldCopyToolView.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开工具时出错: {ex.Message}", "错误");
            }
        }
    }
}
