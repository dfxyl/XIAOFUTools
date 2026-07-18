using XIAOFUTools.Features.User.AIAssistant.Infrastructure;

namespace XIAOFUTools.Tests.AIAssistant;

public sealed class AIAssistantUiPathResolverTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Resolver_PrefersCurrentLayoutAndSupportsLegacyLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xiaofutools-ai-ui-{Guid.NewGuid():N}");
        var assemblyPath = Path.Combine(root, "XIAOFUTools.dll");
        var current = Path.Combine(root, "Features", "User", "AIAssistant", "UI", "chat.html");
        var legacy = Path.Combine(root, "Tools", "User", "AIAssistant", "UI", "chat.html");
        var resolver = new AIAssistantUiPathResolver();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
            File.WriteAllText(legacy, "legacy");
            Assert.Equal(Path.GetFullPath(legacy), resolver.ResolveChatHtmlPath(assemblyPath));

            Directory.CreateDirectory(Path.GetDirectoryName(current)!);
            File.WriteAllText(current, "current");
            Assert.Equal(Path.GetFullPath(current), resolver.ResolveChatHtmlPath(assemblyPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
