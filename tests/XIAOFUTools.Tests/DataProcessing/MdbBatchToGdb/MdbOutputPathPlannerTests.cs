using XIAOFUTools.Features.DataManagement.MdbBatchToGdb;

namespace XIAOFUTools.Tests.DataProcessing.MdbBatchToGdb;

public sealed class MdbOutputPathPlannerTests
{
    [Theory]
    [InlineData((int)MdbOutputFormat.FileGeodatabase, ".gdb")]
    [InlineData((int)MdbOutputFormat.MobileGeodatabase, ".geodatabase")]
    [InlineData((int)MdbOutputFormat.XmlWorkspaceDocument, ".xml")]
    public void BuildPaths_UsesExtensionForSelectedOutputFormat(int outputFormatValue, string extension)
    {
        var outputFormat = (MdbOutputFormat)outputFormatValue;
        var paths = MdbOutputPathPlanner.BuildPaths(
            new[] { new MdbOutputPathRequest(@"D:\input\survey.mdb", "survey") },
            saveToSourcePath: false,
            inputFolderPath: @"D:\input",
            outputFolderPath: @"D:\output",
            outputFormat);

        var path = Assert.Single(paths);

        Assert.Equal(@$"D:\output\survey{extension}", path.OutputPath);
    }

    [Theory]
    [InlineData((int)MdbOutputFormat.FileGeodatabase, "FILE_GDB")]
    [InlineData((int)MdbOutputFormat.MobileGeodatabase, "MOBILE_GDB")]
    [InlineData((int)MdbOutputFormat.XmlWorkspaceDocument, "XML")]
    public void GetGeoprocessingOutputFormat_ReturnsNativeToolValue(int outputFormatValue, string toolValue)
    {
        Assert.Equal(toolValue, ((MdbOutputFormat)outputFormatValue).GetGeoprocessingOutputFormat());
    }

    [Fact]
    public void BuildPaths_AppendsSuffixWhenOutputNamesConflict()
    {
        var paths = MdbOutputPathPlanner.BuildPaths(
            new[]
            {
                new MdbOutputPathRequest(@"D:\input\a\survey.mdb", "survey"),
                new MdbOutputPathRequest(@"D:\input\b\survey.mdb", "survey")
            },
            saveToSourcePath: false,
            inputFolderPath: @"D:\input",
            outputFolderPath: @"D:\output",
            MdbOutputFormat.MobileGeodatabase);

        Assert.Equal(
            new[] { @"D:\output\survey.geodatabase", @"D:\output\survey_2.geodatabase" },
            paths.Select(path => path.OutputPath));
    }
}
