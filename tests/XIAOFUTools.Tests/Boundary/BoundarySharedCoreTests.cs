using XIAOFUTools.Features.Editing.Boundary.Shared.Core;

namespace XIAOFUTools.Tests.Boundary;

public sealed class BoundarySharedCoreTests
{
    [Fact]
    public void Normalize_RemovesConsecutiveAndClosingDuplicatesWithinTolerance()
    {
        var source = new[]
        {
            new SourceVertex("A", 0, 0),
            new SourceVertex("A-duplicate", 0.0004, 0.0004),
            new SourceVertex("B", 10, 0),
            new SourceVertex("C", 10, 5),
            new SourceVertex("closing", 0.0003, 0.0003)
        };

        var result = BoundaryRingNormalizer.Normalize(
            source,
            item => new BoundaryVertex(item.X, item.Y),
            0.001);

        Assert.Equal(new[] { "A", "B", "C" }, result.Select(item => item.Name));
    }

    [Fact]
    public void Measure_ReturnsLengthAndReadableTextAngle()
    {
        var forward = BoundaryEdgeMeasurement.Measure(
            new BoundaryVertex(0, 0),
            new BoundaryVertex(-3, 4));
        var reverse = BoundaryEdgeMeasurement.Measure(
            new BoundaryVertex(0, 0),
            new BoundaryVertex(-3, -4));

        Assert.Equal(5, forward.Length, 12);
        Assert.InRange(forward.TextAngleDegrees, -90, 90);
        Assert.InRange(reverse.TextAngleDegrees, -90, 90);
        Assert.Equal(-53.13010235415598, forward.TextAngleDegrees, 10);
        Assert.Equal(53.13010235415598, reverse.TextAngleDegrees, 10);
    }

    [Fact]
    public void LabelFormatter_PreservesConfiguredAffixesAndZeroPadding()
    {
        Assert.Equal("J-12号", BoundaryLabelFormatter.FormatPointLabel(12, "J-", "号"));
        Assert.Equal("L=2.30m", BoundaryLabelFormatter.FormatEdgeLabel(2.3, 2, true, "L=", "m"));
        Assert.Equal("2.3", BoundaryLabelFormatter.FormatEdgeLabel(2.3, 2, false, string.Empty, string.Empty));
    }

    [Fact]
    public void PointCandidates_StartOutsideRingAndIncludeStableFallbacks()
    {
        var ring = Rectangle();

        var candidates = BoundaryLabelPlacementPlanner.PlanPointCandidates(ring, 0, 2);

        Assert.Equal(33, candidates.Count);
        Assert.False(BoundaryLabelPlacementPlanner.IsPointInPolygon(candidates[0], ring));
        Assert.Equal(new BoundaryVertex(Math.Sqrt(2), Math.Sqrt(2)), candidates[1]);
    }

    [Fact]
    public void EdgeCandidates_StartOutsideRingAndAlternateSides()
    {
        var ring = Rectangle();

        var candidates = BoundaryLabelPlacementPlanner.PlanEdgeCandidates(ring[0], ring[1], 2, ring);

        Assert.Equal(8, candidates.Count);
        Assert.False(BoundaryLabelPlacementPlanner.IsPointInPolygon(candidates[0], ring));
        Assert.Equal(new BoundaryVertex(5, -2), candidates[0]);
        Assert.Equal(new BoundaryVertex(5, 2), candidates[1]);
    }

    [Fact]
    public void Overlap_UsesExistingNinetyPercentThreshold()
    {
        var first = new BoundaryLabelBounds(0, 0, 10, 4);

        Assert.True(BoundaryLabelPlacementPlanner.IsOverlapping(
            first,
            new BoundaryLabelBounds(8, 0, 10, 4)));
        Assert.False(BoundaryLabelPlacementPlanner.IsOverlapping(
            first,
            new BoundaryLabelBounds(9, 0, 10, 4)));
    }

    private static BoundaryVertex[] Rectangle()
    {
        return
        [
            new BoundaryVertex(0, 0),
            new BoundaryVertex(10, 0),
            new BoundaryVertex(10, 5),
            new BoundaryVertex(0, 5)
        ];
    }

    private sealed record SourceVertex(string Name, double X, double Y);
}
