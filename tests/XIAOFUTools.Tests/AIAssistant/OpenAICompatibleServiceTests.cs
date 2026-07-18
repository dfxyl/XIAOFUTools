using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Services;

namespace XIAOFUTools.Tests.AIAssistant;

public class OpenAICompatibleServiceTests
{
    [Fact]
    public async Task SendMessageStreamAsync_UsesSingleChatCompletionsEndpointAndStreamsContent()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                data: {"choices":[{"delta":{"role":"assistant","content":"","reasoning_content":null},"finish_reason":null}]}

                data: {"choices":[{"delta":{"role":null,"content":"你好","reasoning_content":null},"finish_reason":null}]}

                data: {"choices":[{"delta":{"role":null,"content":"，链路正常","reasoning_content":null},"finish_reason":"stop"}]}

                data: [DONE]

                """,
                Encoding.UTF8,
                "text/event-stream")
        });

        var service = CreateService("https://integrate.api.nvidia.com/v1/chat/completions", handler);
        var chunks = new List<string>();

        var result = await service.SendMessageStreamAsync(
            new List<ChatMessage> { new ChatMessage("user", "测试") },
            chunks.Add);

        Assert.Equal("https://integrate.api.nvidia.com/v1/chat/completions", handler.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Contains("text/event-stream", handler.AcceptHeaders);
        Assert.Equal("你好，链路正常", result);
        Assert.Equal(new[] { "你好", "，链路正常" }, chunks);
    }

    [Fact]
    public async Task SendMessageStreamAsync_DropsImagesWhenModelDoesNotSupportVision()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("data: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
        });

        var service = CreateService("https://integrate.api.nvidia.com/v1", handler, supportsVision: false);

        await service.SendMessageStreamAsync(
            new List<ChatMessage> { new ChatMessage("user", "测试", new List<string> { "data:image/png;base64,abc" }) },
            _ => { });

        Assert.NotNull(handler.RequestBody);
        var json = JObject.Parse(handler.RequestBody!);
        var content = json["messages"]?[0]?["content"];
        Assert.Equal("测试", content?.ToString());
    }

    [Fact]
    public async Task SendMessageStreamAsync_ClampsNvidiaBuiltInMaxTokensForFastRequests()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("data: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
        });

        var service = CreateService(
            "https://integrate.api.nvidia.com/v1/chat/completions",
            handler,
            maxTokens: 32768,
            temperature: 9);

        await service.SendMessageStreamAsync(
            new List<ChatMessage> { new ChatMessage("user", "测试") },
            _ => { });

        Assert.NotNull(handler.RequestBody);
        var json = JObject.Parse(handler.RequestBody!);
        Assert.Equal(8192, json["max_tokens"]!.ToObject<int>());
        Assert.Equal(2d, json["temperature"]!.ToObject<double>());
    }

    [Fact]
    public async Task SendMessageStreamAsync_OmitsMaxTokensWhenConfigDoesNotSetPositiveLimit()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("data: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
        });

        var service = CreateService(
            "https://integrate.api.nvidia.com/v1",
            handler,
            maxTokens: 0);

        await service.SendMessageStreamAsync(
            new List<ChatMessage> { new ChatMessage("user", "测试") },
            _ => { });

        Assert.NotNull(handler.RequestBody);
        var json = JObject.Parse(handler.RequestBody!);
        Assert.Null(json["max_tokens"]);
    }

    [Fact]
    public async Task SendWithToolsStreamAsync_UsesFinalMessageTextWhenDeltaContainsNoDisplayText()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                data: {"choices":[{"delta":{"role":"assistant","content":null},"message":{"content":[{"type":"output_text","text":"模型回复正常"}]},"finish_reason":"stop"}]}

                data: [DONE]

                """,
                Encoding.UTF8,
                "text/event-stream")
        });

        var service = CreateService("https://integrate.api.nvidia.com/v1", handler);
        var chunks = new List<string>();

        var result = await service.SendWithToolsStreamAsync(
            new List<ChatMessage> { new ChatMessage("user", "测试") },
            new List<AIToolDefinition>(),
            chunks.Add);

        Assert.Equal("模型回复正常", result.Content);
        Assert.Equal("stop", result.FinishReason);
        Assert.Equal(new[] { "模型回复正常" }, chunks);
    }

    private static OpenAICompatibleService CreateService(
        string endpoint,
        CapturingHandler handler,
        bool supportsVision = true,
        int maxTokens = 128,
        double temperature = 0.2,
        string modelName = "minimaxai/minimax-m3")
    {
        var config = new AIServiceConfig
        {
            ApiEndpoint = endpoint,
            ApiKey = "test-key",
            ModelName = modelName,
            MaxTokens = maxTokens,
            Temperature = temperature,
            SupportsVision = supportsVision
        };

        return new OpenAICompatibleService(config, new HttpClient(handler));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? RequestBody { get; private set; }
        public string AcceptHeaders { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            AcceptHeaders = string.Join(",", request.Headers.Accept.Select(h => h.MediaType));
            RequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responseFactory(request);
        }
    }
}
