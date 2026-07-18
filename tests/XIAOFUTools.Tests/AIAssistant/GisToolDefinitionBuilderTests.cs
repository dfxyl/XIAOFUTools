using Newtonsoft.Json.Linq;
using XIAOFUTools.Features.User.AIAssistant.Agent;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tooling;

namespace XIAOFUTools.Tests.AIAssistant;

public sealed class GisToolDefinitionBuilderTests
{
    private readonly IReadOnlyDictionary<string, IGISTool> _tools =
        new Dictionary<string, IGISTool>(StringComparer.OrdinalIgnoreCase)
        {
            ["alpha"] = new FakeTool("alpha"),
            ["beta"] = new FakeTool("beta")
        };

    [Fact]
    public void Build_ChatModeWithoutPreferredToolExposesNoTools()
    {
        var definitions = GisToolDefinitionBuilder.Build(
            _tools,
            string.Empty,
            "chat",
            _ => true);

        Assert.Empty(definitions);
    }

    [Fact]
    public void Build_AgentModeIncludesOnlyAvailableTools()
    {
        var definitions = GisToolDefinitionBuilder.Build(
            _tools,
            string.Empty,
            "agent",
            name => name == "beta");

        var definition = Assert.Single(definitions);
        Assert.Equal("beta", definition.Name);
        Assert.Equal("beta description", definition.Description);
        Assert.Equal("object", definition.ParametersSchema["type"]?.ToString());
    }

    [Fact]
    public void Build_PreferredToolRestrictsDefinitionInAnyMode()
    {
        var definitions = GisToolDefinitionBuilder.Build(
            _tools,
            "alpha",
            "chat",
            name => name == "alpha");

        Assert.Equal("alpha", Assert.Single(definitions).Name);
    }

    [Fact]
    public void BuildChoice_UsesAutoOrOpenAiFunctionShape()
    {
        Assert.Equal("auto", GisToolDefinitionBuilder.BuildChoice(string.Empty));

        var choice = Assert.IsType<JObject>(GisToolDefinitionBuilder.BuildChoice("alpha"));
        Assert.Equal("function", choice["type"]?.ToString());
        Assert.Equal("alpha", choice["function"]?["name"]?.ToString());
    }

    private sealed class FakeTool : IGISTool
    {
        internal FakeTool(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string Description => $"{Name} description";

        public JObject ParametersSchema { get; } = new JObject
        {
            ["type"] = "object"
        };

        public Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            return Task.FromResult(ToolResult.CreateSuccess("ok"));
        }
    }
}
