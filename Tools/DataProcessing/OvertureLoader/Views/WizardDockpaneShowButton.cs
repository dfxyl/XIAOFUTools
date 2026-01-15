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
using ArcGIS.Desktop.KnowledgeGraph;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.OvertureLoader.Views
{
    /// <summary>
    /// Button implementation to show the Overture Maps Wizard Dockpane
    /// </summary>
    internal class WizardDockpaneShowButton : Button
    {
        /// <summary>
        /// Called when the button is clicked
        /// </summary>
        protected override void OnClick()
        {
            if (!AuthorizationChecker.CheckAuthorizationWithPrompt("Overture Maps数据加载器"))
                return;
            try
            {
                // Log that button was clicked
                System.Diagnostics.Debug.WriteLine("Overture Maps wizard button clicked");

                // Show the dockpane
                WizardDockpaneViewModel.Show();
            }
            catch (Exception ex)
            {
                // Log any errors that occur
                System.Diagnostics.Debug.WriteLine($"Error showing Overture Maps wizard: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Show a friendly error message
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"无法打开Overture Maps数据加载器。\n\n错误详情: {ex.Message}\n\n请检查ArcGIS Pro日志了解更多详细信息。",
                    "打开Overture Maps时出错",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Called by the framework to determine whether the button should be enabled or disabled
        /// </summary>
        protected override void OnUpdate()
        {
            // Button is always enabled
            Enabled = true;
        }
    }
}
