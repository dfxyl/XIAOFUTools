using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;

namespace XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable
{
    public partial class LayoutCoordinateTableView : UserControl
    {
        public LayoutCoordinateTableView()
        {
            InitializeComponent();
            InitializeAsync();
        }
    }
}
