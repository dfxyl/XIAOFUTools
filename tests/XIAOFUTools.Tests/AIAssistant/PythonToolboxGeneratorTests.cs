using XIAOFUTools.Features.User.AIAssistant.Agent.Tools;
using XIAOFUTools.Features.User.AIAssistant.Database;

namespace XIAOFUTools.Tests.AIAssistant;

public class PythonToolboxGeneratorTests
{
    [Fact]
    public void BuildToolboxContent_GeneratesPythonToolboxWithParametersAndExecuteBody()
    {
        var request = new PythonToolboxDefinition
        {
            ToolboxLabel = "AI Generated Tools",
            ToolClassName = "CountFeaturesTool",
            ToolLabel = "Count Features",
            Description = "Count features from an input feature class.",
            ExecuteCode = "arcpy.AddMessage('done')",
            Parameters =
            [
                new PythonToolboxParameterDefinition
                {
                    Name = "input_features",
                    DisplayName = "Input Features",
                    Datatype = "DEFeatureClass",
                    ParameterType = "Required",
                    Direction = "Input"
                },
                new PythonToolboxParameterDefinition
                {
                    Name = "summary",
                    DisplayName = "Summary",
                    Datatype = "GPString",
                    ParameterType = "Derived",
                    Direction = "Output"
                }
            ]
        };

        var content = PythonToolboxGenerator.BuildToolboxContent(request);

        Assert.Contains("class Toolbox(object):", content);
        Assert.Contains("self.tools = [CountFeaturesTool]", content);
        Assert.Contains("class CountFeaturesTool(object):", content);
        Assert.Contains("input_features = arcpy.Parameter(", content);
        Assert.Contains("summary = arcpy.Parameter(", content);
        Assert.Contains("self.params_by_name = {p.name: p for p in parameters}", content);
        Assert.Contains("arcpy.AddMessage('done')", content);
    }

    [Theory]
    [InlineData("../bad", "AiTool", "bad")]
    [InlineData("1 invalid name", "T", "T_1_invalid_name")]
    [InlineData("中文工具", "AiTool", "AiTool")]
    public void SanitizePythonIdentifier_ReturnsSafeClassName(string input, string fallbackPrefix, string expected)
    {
        Assert.Equal(expected, PythonToolboxGenerator.SanitizePythonIdentifier(input, fallbackPrefix));
    }

    [Fact]
    public void ToolCatalog_EnablesPythonToolboxGenerationForAgentMode()
    {
        var descriptor = AIAssistantToolCatalog.Find("create_python_toolbox");

        Assert.NotNull(descriptor);
        Assert.False(descriptor.DefaultChatEnabled);
        Assert.True(descriptor.DefaultAgentEnabled);
        Assert.True(descriptor.RequiresSafetyWarning);
    }
}
