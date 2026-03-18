using XIAOFUTools.Tools.QuickAddData;

namespace XIAOFUTools.Tests.QuickAddData;

public sealed class QuickDataTreeSelectionHelperTests
{
    [Fact]
    public void BuildRangeSelection_SelectsClosedInterval()
    {
        var selection = QuickDataTreeSelectionHelper.BuildRangeSelection(
            ["a", "b", "c", "d"],
            "b",
            "d",
            [],
            additive: false);

        Assert.Equal(["b", "c", "d"], selection.OrderBy(item => item).ToArray());
    }

    [Fact]
    public void BuildRangeSelection_CanAddToExistingSelection()
    {
        var selection = QuickDataTreeSelectionHelper.BuildRangeSelection(
            ["a", "b", "c", "d"],
            "b",
            "c",
            ["a"],
            additive: true);

        Assert.Equal(["a", "b", "c"], selection.OrderBy(item => item).ToArray());
    }
}
