using XIAOFUTools.Tools.QuickAddData;

namespace XIAOFUTools.Tests.QuickAddData;

public sealed class QuickDataDuplicateDetectorTests
{
    [Fact]
    public void TryRegister_SkipsExistingAndRepeatedKeys()
    {
        var existingKey = QuickDataNode.BuildUniqueKey(@"D:\Data", "roads.shp", QuickDataNodeKind.FeatureClass);
        var detector = new QuickDataDuplicateDetector([existingKey]);

        var roads = new QuickDataNode
        {
            Name = "roads",
            NodeKind = QuickDataNodeKind.FeatureClass,
            GeometryKind = QuickDataGeometryKind.Polyline,
            ContainerPath = @"D:\Data",
            DatasetName = "roads.shp",
            SourcePath = @"D:\Data\roads.shp",
            UniqueKey = existingKey
        };

        var buildings = new QuickDataNode
        {
            Name = "buildings",
            NodeKind = QuickDataNodeKind.FeatureClass,
            GeometryKind = QuickDataGeometryKind.Polygon,
            ContainerPath = @"D:\Data",
            DatasetName = "buildings.shp",
            SourcePath = @"D:\Data\buildings.shp",
            UniqueKey = QuickDataNode.BuildUniqueKey(@"D:\Data", "buildings.shp", QuickDataNodeKind.FeatureClass)
        };

        var buildingsClone = new QuickDataNode
        {
            Name = "buildings",
            NodeKind = QuickDataNodeKind.FeatureClass,
            GeometryKind = QuickDataGeometryKind.Polygon,
            ContainerPath = @"D:\Data",
            DatasetName = "buildings.shp",
            SourcePath = @"D:\Data\buildings.shp",
            UniqueKey = buildings.UniqueKey
        };

        Assert.False(detector.TryRegister(roads));
        Assert.True(detector.TryRegister(buildings));
        Assert.False(detector.TryRegister(buildingsClone));
        Assert.Equal(2, detector.SkippedCount);
    }
}
