using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Features.User.AIAssistant.Database;

#nullable disable

namespace XIAOFUTools.Features.User.AIAssistant.Services
{
    /// <summary>
    /// OpenAI兼容API服务实现(支持DeepSeek、SiliconFlow等)
    /// </summary>
    public class OpenAICompatibleService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly AIServiceConfig _config;
        private readonly bool _ownsHttpClient;

        private bool RequiresGlmThinkingParam()
        {
            return _config.ModelName.Equals("zai-org/GLM-4.6V", StringComparison.OrdinalIgnoreCase)
                   || _config.ModelName.Equals("zai-org/GLM-4.5V", StringComparison.OrdinalIgnoreCase);
        }

        private bool RequiresReasoningContentForToolMessages()
        {
            return _config.ModelName.IndexOf("deepseek-reasoner", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public OpenAICompatibleService(AIServiceConfig config)
            : this(config, new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            }, true)
        {
        }

        internal OpenAICompatibleService(AIServiceConfig config, HttpClient httpClient)
            : this(config, httpClient, false)
        {
        }

        private OpenAICompatibleService(AIServiceConfig config, HttpClient httpClient, bool ownsHttpClient)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _ownsHttpClient = ownsHttpClient;
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        }

        public async Task<string> SendMessageStreamAsync(
            List<ChatMessage> messages,
            Action<string> onChunkReceived,
            CancellationToken cancellationToken = default,
            Action<string> onReasoningReceived = null)
        {
            var requestBody = BuildRequestBody(messages, stream: true);

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");

            var endpoint = GetChatCompletionsEndpoint();
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            var response = await SendAsyncWithDiagnosticsAsync(request, stream: true, cancellationToken);
            
            // 改进的错误处理，显示详细错误信息
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"API请求失败: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"错误内容: {errorContent}");
                throw new HttpRequestException($"API请求失败 ({response.StatusCode}): {errorContent}");
            }

            var fullResponse = new StringBuilder();
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await ReadSseLineAsync(reader, cancellationToken);
                    if (line == null)
                        break;

                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                        continue;

                    var data = line.Substring(6).Trim();
                    if (data == "[DONE]")
                        break;

                    try
                    {
                        var json = JObject.Parse(data);
                        var choice = json["choices"]?[0] as JObject;
                        var delta = choice?["delta"] as JObject;
                        var message = choice?["message"] as JObject;
                        
                        // 处理正常内容
                        var content_chunk = ExtractFirstDisplayText(
                            delta?["content"],
                            delta?["text"],
                            delta?["output_text"],
                            message?["content"],
                            message?["output_text"],
                            choice?["text"],
                            choice?["output_text"]);
                        if (!string.IsNullOrEmpty(content_chunk))
                        {
                            fullResponse.Append(content_chunk);
                            onChunkReceived?.Invoke(content_chunk);
                        }
                        
                        // 处理DeepSeek Reasoner的思考内容
                        var reasoning_chunk = ExtractFirstDisplayText(
                            delta?["reasoning_content"],
                            delta?["reasoning"],
                            message?["reasoning_content"],
                            message?["reasoning"]);
                        if (!string.IsNullOrEmpty(reasoning_chunk))
                        {
                            onReasoningReceived?.Invoke(reasoning_chunk);
                        }
                    }
                    catch (JsonException)
                    {
                        // 忽略解析错误
                    }
                }
            }

            return fullResponse.ToString();
        }

        public async Task<string> SendMessageAsync(
            List<ChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            var requestBody = BuildRequestBody(messages, stream: false);

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");

            var endpoint = GetChatCompletionsEndpoint();
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await SendAsyncWithDiagnosticsAsync(request, stream: false, cancellationToken);
            
            // 改进的错误处理，显示详细错误信息
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"API请求失败: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"错误内容: {errorContent}");
                throw new HttpRequestException($"API请求失败 ({response.StatusCode}): {errorContent}");
            }

            var responseText = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseText);
            var choice = json["choices"]?[0] as JObject;
            var message = choice?["message"] as JObject;
            return ExtractFirstDisplayText(
                message?["content"],
                message?["output_text"],
                choice?["text"],
                choice?["output_text"],
                json["output_text"]);
        }

        public async Task<AICompletionResult> SendWithToolsAsync(
            List<ChatMessage> messages,
            List<AIToolDefinition> tools,
            object toolChoice = null,
            CancellationToken cancellationToken = default)
        {
            var requestBody = BuildRequestBody(messages, stream: false, tools, toolChoice);

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");

            var endpoint = GetChatCompletionsEndpoint();
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await SendAsyncWithDiagnosticsAsync(request, stream: false, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"API请求失败: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"错误内容: {errorContent}");
                throw new HttpRequestException($"API请求失败 ({response.StatusCode}): {errorContent}");
            }

            var responseText = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseText);
            var choice = json["choices"]?[0] as JObject;
            if (choice == null)
            {
                return new AICompletionResult
                {
                    Content = string.Empty,
                    FinishReason = "stop"
                };
            }

            var message = choice["message"] as JObject;
            var result = new AICompletionResult
            {
                Content = ExtractFirstDisplayText(
                    message?["content"],
                    message?["output_text"],
                    choice?["text"],
                    choice?["output_text"],
                    json["output_text"]),
                ReasoningContent = ExtractFirstDisplayText(
                    message?["reasoning_content"],
                    message?["reasoning"]),
                FinishReason = choice["finish_reason"]?.ToString() ?? "stop",
                ToolCalls = ParseToolCalls(message?["tool_calls"] as JArray)
            };

            return result;
        }

        public async Task<AICompletionResult> SendWithToolsStreamAsync(
            List<ChatMessage> messages,
            List<AIToolDefinition> tools,
            Action<string> onChunkReceived,
            Action<string> onReasoningReceived = null,
            object toolChoice = null,
            CancellationToken cancellationToken = default)
        {
            var requestBody = BuildRequestBody(messages, stream: true, tools, toolChoice);

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");

            var endpoint = GetChatCompletionsEndpoint();
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            var response = await SendAsyncWithDiagnosticsAsync(request, stream: true, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"API请求失败: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"错误内容: {errorContent}");
                throw new HttpRequestException($"API请求失败 ({response.StatusCode}): {errorContent}");
            }

            var fullResponse = new StringBuilder();
            var fullReasoning = new StringBuilder();
            var toolCallParts = new Dictionary<int, ToolCallBuilder>();
            var finishReason = "stop";

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await ReadSseLineAsync(reader, cancellationToken);
                    if (line == null)
                        break;

                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                    {
                        continue;
                    }

                    var data = line.Substring(6).Trim();
                    if (data == "[DONE]")
                    {
                        break;
                    }

                    try
                    {
                        var json = JObject.Parse(data);
                        var choice = json["choices"]?[0] as JObject;
                        if (choice == null)
                        {
                            continue;
                        }

                        finishReason = choice["finish_reason"]?.ToString() ?? finishReason;
                        var delta = choice["delta"] as JObject;
                        var message = choice["message"] as JObject;
                        var contentChunk = ExtractFirstDisplayText(
                            delta?["content"],
                            delta?["text"],
                            delta?["output_text"],
                            message?["content"],
                            message?["output_text"],
                            choice["text"],
                            choice["output_text"]);
                        if (!string.IsNullOrEmpty(contentChunk))
                        {
                            fullResponse.Append(contentChunk);
                            onChunkReceived?.Invoke(contentChunk);
                        }

                        var reasoningChunk = ExtractFirstDisplayText(
                            delta?["reasoning_content"],
                            delta?["reasoning"],
                            message?["reasoning_content"],
                            message?["reasoning"]);
                        if (!string.IsNullOrEmpty(reasoningChunk))
                        {
                            fullReasoning.Append(reasoningChunk);
                            onReasoningReceived?.Invoke(reasoningChunk);
                        }

                        var deltaToolCalls = delta?["tool_calls"] as JArray;
                        if (deltaToolCalls != null)
                        {
                            foreach (var toolCall in deltaToolCalls.OfType<JObject>())
                            {
                                var index = toolCall["index"]?.ToObject<int?>() ?? 0;
                                if (!toolCallParts.TryGetValue(index, out var part))
                                {
                                    part = new ToolCallBuilder();
                                    toolCallParts[index] = part;
                                }

                                var toolId = toolCall["id"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(toolId))
                                {
                                    part.Id = toolId;
                                }

                                var function = toolCall["function"] as JObject;
                                if (function != null)
                                {
                                    var name = function["name"]?.ToString();
                                    if (!string.IsNullOrWhiteSpace(name))
                                    {
                                        part.Name = name;
                                    }

                                    var argumentsChunk = function["arguments"]?.ToString();
                                    if (!string.IsNullOrEmpty(argumentsChunk))
                                    {
                                        part.Arguments.Append(argumentsChunk);
                                    }
                                }
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // 忽略流式解析异常，继续读取后续片段
                    }
                }
            }

            var toolCalls = toolCallParts
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new AIToolCall
                {
                    Id = string.IsNullOrWhiteSpace(kvp.Value.Id) ? Guid.NewGuid().ToString("N") : kvp.Value.Id,
                    Name = kvp.Value.Name,
                    Arguments = kvp.Value.Arguments.Length == 0 ? "{}" : kvp.Value.Arguments.ToString()
                })
                .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                .ToList();

            return new AICompletionResult
            {
                Content = fullResponse.ToString(),
                ReasoningContent = fullReasoning.ToString(),
                FinishReason = finishReason,
                ToolCalls = toolCalls
            };
        }

        public async Task<bool> ValidateApiKeyAsync()
        {
            try
            {
                var testMessages = new List<ChatMessage>
                {
                    new ChatMessage("user", "Hello")
                };

                await SendMessageAsync(testMessages);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
            {
                _httpClient?.Dispose();
            }
        }

        private Dictionary<string, object> BuildRequestBody(
            List<ChatMessage> messages,
            bool stream,
            List<AIToolDefinition> tools = null,
            object toolChoice = null)
        {
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = _config.ModelName,
                ["messages"] = messages.Select(m => ConvertMessageToApiFormat(m, RequiresReasoningContentForToolMessages(), _config.SupportsVision)).ToList(),
                ["temperature"] = GetEffectiveTemperature(),
                ["stream"] = stream
            };

            var effectiveMaxTokens = GetEffectiveMaxTokens();
            if (effectiveMaxTokens > 0)
            {
                requestBody["max_tokens"] = effectiveMaxTokens;
            }

            if (tools != null && tools.Count > 0)
            {
                requestBody["tools"] = tools.Select(t => new
                {
                    type = "function",
                    function = new
                    {
                        name = t.Name,
                        description = t.Description,
                        parameters = t.ParametersSchema
                    }
                }).ToList();

                requestBody["tool_choice"] = toolChoice ?? "auto";
            }

            if (RequiresGlmThinkingParam())
            {
                requestBody["enable_thinking"] = true;
            }

            return requestBody;
        }

        private int GetEffectiveMaxTokens()
        {
            if (_config.MaxTokens <= 0)
            {
                return 0;
            }

            if (AIAssistantDefaultServicePolicy.IsNvidiaBuiltInService(_config))
            {
                return Math.Min(_config.MaxTokens, AIAssistantDefaultServicePolicy.NvidiaMaxRequestTokens);
            }

            return _config.MaxTokens;
        }

        private double GetEffectiveTemperature()
        {
            if (double.IsNaN(_config.Temperature) || double.IsInfinity(_config.Temperature))
            {
                return 0.7;
            }

            return Math.Max(0, Math.Min(2, _config.Temperature));
        }

        private async Task<HttpResponseMessage> SendAsyncWithDiagnosticsAsync(
            HttpRequestMessage request,
            bool stream,
            CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var endpoint = request.RequestUri?.ToString() ?? string.Empty;
            System.Diagnostics.Debug.WriteLine($"[OpenAICompatibleService] 请求开始 model={_config.ModelName}, stream={stream}, endpoint={endpoint}, timeout=none");

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            stopwatch.Stop();
            System.Diagnostics.Debug.WriteLine($"[OpenAICompatibleService] 响应头 model={_config.ModelName}, status={(int)response.StatusCode}, elapsed={stopwatch.ElapsedMilliseconds}ms");
            return response;
        }

        private async Task<string> ReadSseLineAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            return await reader.ReadLineAsync(cancellationToken);
        }

        private string GetChatCompletionsEndpoint()
        {
            var endpoint = (_config.ApiEndpoint ?? string.Empty).Trim().TrimEnd('/');
            if (endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return endpoint;
            }

            return endpoint + "/chat/completions";
        }

        private static string ExtractFirstDisplayText(params JToken[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var text = ExtractDisplayText(candidate);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private static string ExtractDisplayText(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return string.Empty;
            }

            if (token is JValue value)
            {
                return value.Type == JTokenType.String ? value.Value<string>() ?? string.Empty : string.Empty;
            }

            if (token is JArray array)
            {
                var content = new StringBuilder();
                foreach (var item in array)
                {
                    content.Append(ExtractDisplayText(item));
                }

                return content.ToString();
            }

            if (token is JObject obj)
            {
                return ExtractFirstDisplayText(obj["text"], obj["content"], obj["output_text"]);
            }

            return string.Empty;
        }

        private static object ConvertMessageToApiFormat(ChatMessage message, bool includeReasoningContent, bool allowImages)
        {
            if (message == null)
            {
                return new { role = "user", content = string.Empty };
            }

            if (string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                && message.ToolCalls != null
                && message.ToolCalls.Count > 0)
            {
                var toolCalls = message.ToolCalls.Select(tc => new
                {
                    id = tc.Id,
                    type = "function",
                    function = new
                    {
                        name = tc.Name,
                        arguments = tc.Arguments ?? "{}"
                    }
                }).ToList();

                if (includeReasoningContent)
                {
                    return new
                    {
                        role = "assistant",
                        content = message.Content,
                        reasoning_content = message.ReasoningContent ?? string.Empty,
                        tool_calls = toolCalls
                    };
                }

                return new
                {
                    role = "assistant",
                    content = message.Content,
                    tool_calls = toolCalls
                };
            }

            if (string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase))
            {
                return new
                {
                    role = "tool",
                    tool_call_id = message.ToolCallId,
                    content = message.Content?.ToString() ?? string.Empty
                };
            }

            if (allowImages && message.Images != null && message.Images.Count > 0)
            {
                var contentArray = new List<object>
                {
                    new { type = "text", text = message.Content?.ToString() ?? string.Empty }
                };

                foreach (var img in message.Images)
                {
                    contentArray.Add(new { type = "image_url", image_url = new { url = img } });
                }

                return new { role = message.Role, content = (object)contentArray };
            }

            return new
            {
                role = message.Role,
                content = message.Content
            };
        }

        private static List<AIToolCall> ParseToolCalls(JArray toolCalls)
        {
            var results = new List<AIToolCall>();
            if (toolCalls == null)
            {
                return results;
            }

            foreach (var item in toolCalls.OfType<JObject>())
            {
                var function = item["function"] as JObject;
                var name = function?["name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                results.Add(new AIToolCall
                {
                    Id = item["id"]?.ToString() ?? Guid.NewGuid().ToString("N"),
                    Name = name,
                    Arguments = function?["arguments"]?.ToString() ?? "{}"
                });
            }

            return results;
        }

        private sealed class ToolCallBuilder
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public StringBuilder Arguments { get; } = new StringBuilder();
        }
    }
}
