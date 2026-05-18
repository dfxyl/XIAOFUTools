using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Windows.Input;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.BrowseFeatures
{
    internal class BrowseFeaturesDockPane : DockPane
    {
        private const string DockPaneId = "XIAOFUTools_BrowseFeaturesDockPane";
        private ICommand _helpCmd;

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new BrowseFeaturesDockPaneView();
        }

        internal static void Show()
        {
            FrameworkApplication.DockPaneManager.Find(DockPaneId)?.Activate();
        }

        public bool HasHelp => true;

        public ICommand HelpCmd => _helpCmd ??= new SimpleRelayCommand(ShowHelp);

        private void ShowHelp()
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                "浏览要素工具说明\n\n" +
                "1. 选择目标图层、遍历范围和排序规则。\n" +
                "2. 点击“刷新快照”生成遍历列表。\n" +
                "3. 使用导航按钮或方向键浏览要素，地图会自动定位并高亮。\n" +
                "4. 审阅状态和备注会实时写入审阅清单表。",
                "浏览要素帮助");
        }
    }
}
