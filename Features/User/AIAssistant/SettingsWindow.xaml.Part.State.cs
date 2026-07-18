using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework.Controls;
using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Services;

namespace XIAOFUTools.Features.User.AIAssistant
{
    public partial class SettingsWindow
    {

        private RadioButton ToolNavButton => FindName("navTool") as RadioButton;
        private ScrollViewer ToolPanel => FindName("panelTool") as ScrollViewer;
        private ComboBox ToolRunModeInChatComboBox => FindName("cmbToolRunModeInChat") as ComboBox;
        private CheckBox SensitiveModeCheckBox => FindName("chkSensitiveMode") as CheckBox;
        private StackPanel ToolListPanel => FindName("toolListPanel") as StackPanel;
    }
}
