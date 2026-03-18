using XIAOFUTools.Tools.QuickAddData;

namespace XIAOFUTools.Tests.QuickAddData;

public sealed class QuickDataDragPathCollectorTests
{
    [Fact]
    public void Collect_SkipsContainersAndReturnsLeafPaths()
    {
        var geodatabase = new QuickDataNode
        {
            Name = "test",
            NodeKind = QuickDataNodeKind.Geodatabase,
            SourcePath = @"D:\Data\test.gdb",
            Children =
            {
                new QuickDataNode
                {
                    Name = "dataset",
                    NodeKind = QuickDataNodeKind.FeatureDataset,
                    SourcePath = @"D:\Data\test.gdb\dataset",
                    Children =
                    {
                        new QuickDataNode
                        {
                            Name = "parcel",
                            NodeKind = QuickDataNodeKind.FeatureClass,
                            SourcePath = @"D:\Data\test.gdb\dataset\parcel"
                        }
                    }
                },
                new QuickDataNode
                {
                    Name = "table1",
                    NodeKind = QuickDataNodeKind.Table,
                    SourcePath = @"D:\Data\test.gdb\table1"
                }
            }
        };

        var paths = QuickDataDragPathCollector.Collect(geodatabase);

        Assert.Equal(
            [@"D:\Data\test.gdb\dataset\parcel", @"D:\Data\test.gdb\table1"],
            paths);
    }
}
