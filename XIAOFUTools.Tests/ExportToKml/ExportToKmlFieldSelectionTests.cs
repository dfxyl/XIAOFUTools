using XIAOFUTools.Tools.ExportToKml;

namespace XIAOFUTools.Tests.ExportToKml;

public sealed class ExportToKmlFieldSelectionTests
{
    [Fact]
    public void ResolveSelectedLabelField_KeepsExistingSelectionWhenFieldStillExists()
    {
        var selected = ExportToKmlFieldSelection.ResolveSelectedLabelField(
            previousSelectedLabelField: "DLMC",
            availableLabelFields: new[] { "OBJECTID", "DLBM", "DLMC" });

        Assert.Equal("DLMC", selected);
    }
}
