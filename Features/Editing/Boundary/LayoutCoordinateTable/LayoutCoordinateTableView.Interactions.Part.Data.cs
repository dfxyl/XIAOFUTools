using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable
{
    public partial class LayoutCoordinateTableView
    {

    private string GetTextBoxText(string name)
    {
        return ((FindName(name)is TextBox tb) ? tb.Text : null) ?? string.Empty;
    }

    private void LayoutCoordinateTableView_Loaded(object sender, RoutedEventArgs e)
    {
        LayerComboBox.SelectionChanged += LayerComboBox_SelectionChanged;
    }

    private List<double> CalculateEdgeLengths(List<(double X, double Y, string XStr, string YStr)> coordinates)
    {
        List<double> edgeLengths = new List<double>();
        for (int i = 0; i < coordinates.Count - 1; i++)
        {
            double dx = coordinates[i + 1].X - coordinates[i].X;
            double dy = coordinates[i + 1].Y - coordinates[i].Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            edgeLengths.Add(length);
        }

        return edgeLengths;
    }
    }
}
