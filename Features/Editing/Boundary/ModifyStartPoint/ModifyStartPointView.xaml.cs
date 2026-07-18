using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace XIAOFUTools.Features.Editing.Boundary.ModifyStartPoint
{
    public partial class ModifyStartPointView : UserControl
    {
        public ModifyStartPointView()
        {
            InitializeComponent();
            LoadPolygonLayers();
            LoadCornerOptions();

            // 设置默认角度阈值
            AngleThresholdTextBox.Text = "179";
        }
    }
}
