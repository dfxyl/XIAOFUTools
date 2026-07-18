using System.Xml.Linq;

namespace XIAOFUTools.Tests.ExportToKml;

public sealed class ExportToKmlDockPaneViewXamlTests
{
    [Fact]
    public void LabelFieldComboBox_UsesSharedComboBoxStyleAndTwoWaySelection()
    {
        var viewPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "Features",
            "Conversion",
            "ExportToKml",
            "ExportToKmlDockPaneView.xaml"));
        var document = XDocument.Load(viewPath);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var comboBox = document
            .Descendants(xaml + "ComboBox")
            .Single(element => (string?)element.Attribute("ItemsSource") == "{Binding LabelFields}");

        Assert.Equal("{StaticResource ComboBoxStyle}", (string?)comboBox.Attribute("Style"));
        Assert.Equal(
            "{Binding SelectedLabelField, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            (string?)comboBox.Attribute("SelectedItem"));
        Assert.Equal(
            "LabelFieldComboBox_PreviewMouseLeftButtonDown",
            (string?)comboBox.Attribute("PreviewMouseLeftButtonDown"));
    }
}
