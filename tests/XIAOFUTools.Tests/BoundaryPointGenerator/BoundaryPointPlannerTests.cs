using XIAOFUTools.Features.DataManagement.BoundaryPointGenerator.Core;

namespace XIAOFUTools.Tests.BoundaryPointGenerator;

public sealed class BoundaryPointPlannerTests
{
    private static readonly BoundaryCoordinate[] Rectangle =
    {
        new(0, 0),
        new(10, 0),
        new(10, 5),
        new(0, 5),
        new(0, 0),
        new(10.00001, 5.00001)
    };

    [Fact]
    public void PlanCardinal_ReturnsDirectionsInStableOrder()
    {
        var result = new BoundaryPointPlanner().PlanCardinal(Rectangle);

        Assert.Equal(new[] { "东", "西", "南", "北" }, result.Select(point => point.Direction));
        Assert.Equal(new BoundaryCoordinate(10, 0), result[0].Coordinate);
        Assert.Equal(new BoundaryCoordinate(0, 0), result[1].Coordinate);
        Assert.Equal(new BoundaryCoordinate(0, 0), result[2].Coordinate);
        Assert.Equal(new BoundaryCoordinate(10, 5), result[3].Coordinate);
    }

    [Fact]
    public void PlanCorners_UsesDirectionalScoresAndStableTieBreaking()
    {
        var result = new BoundaryPointPlanner().PlanCorners(Rectangle);

        Assert.Equal(new[] { "东北", "西北", "东南", "西南" }, result.Select(point => point.Direction));
        Assert.Equal(new BoundaryCoordinate(10, 5), result[0].Coordinate);
        Assert.Equal(new BoundaryCoordinate(0, 5), result[1].Coordinate);
        Assert.Equal(new BoundaryCoordinate(10, 0), result[2].Coordinate);
        Assert.Equal(new BoundaryCoordinate(0, 0), result[3].Coordinate);
    }

    [Fact]
    public void EmptyInput_ReturnsNoPlannedPoints()
    {
        var planner = new BoundaryPointPlanner();

        Assert.Empty(planner.PlanCardinal(Array.Empty<BoundaryCoordinate>()));
        Assert.Empty(planner.PlanCorners(Array.Empty<BoundaryCoordinate>()));
    }
}
