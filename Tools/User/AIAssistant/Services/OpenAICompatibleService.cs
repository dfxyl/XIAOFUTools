using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Tools.User.AIAssistant.Database;

namespace XIAOFUTools.Tools.User.AIAssistant.Services
{
    /// <summary>
    /// OpenAI兼容API服务实现(支持DeepSeek、SiliconFlow等)
    /// </summary>
    public class OpenAICompatibleService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly AIServiceConfig _config;

        public OpenAICompatibleService(AIServiceConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        }

        public async Task<string> SendMessageStreamAsync(
            List<ChatMessage> messages,
            Action<string> onChunkReceived,
            CancellationToken cancellationToken = default,
            Action<string> onReasoningReceived = null)
        {
            // 构建请求体
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = _config.ModelName,
                ["messages"] = messages.Select(m => {
                    // 如果有图片，使用Vision API格式
                    if (m.Images != null && m.Images.Count > 0)
                    {
                        var contentArray = new List<object>();
                        contentArray.Add(new { type = "text", text = m.Content?.ToString() ?? "" });
                        foreach (var img in m.Images)
                        {
                            contentArray.Add(new { type = "image_url", image_url = new { url = img } });
                        }
                        return new { role = m.Role, content = (object)contentArray };
                    }
                    return new { role = m.Role, content = m.Content };
                }).ToList(),
                ["temperature"] = _config.Temperature,
                ["max_tokens"] = _config.MaxTokens,
                ["stream"] = true
            };
            
            // GLM-4.5V需要enable_thinking参数
            if (_config.ModelName.Equals("zai-org/GLM-4.5V", StringComparison.OrdinalIgnoreCase))
            {
                requestBody["enable_thinking"] = true;
            }

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");

            var endpoint = _config.ApiEndpoint.TrimEnd('/') + "/chat/completions";
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
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
                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                        continue;

                    var data = line.Substring(6).Trim();
                    if (data == "[DONE]")
                        break;

                    try
                    {
                        var json = JObject.Parse(data);
                        var delta = json["choices"]?[0]?["delta"];
                        
                        // 处理正常内容
                        var content_chunk = delta?["content"]?.ToString();
                        if (!string.IsNullOrEmpty(content_chunk))
                        {
                            fullResponse.Append(content_chunk);
                            onChunkReceived?.Invoke(content_chunk);
                        }
                        
                        // 处理DeepSeek Reasoner的思考内容
                        var reasoning_chunk = delta?["reasoning_content"]?.ToString();
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
            // 构建请求体
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = _config.ModelName,
                ["messages"] = messages.Select(m => {
                    // 如果有图片，使用Vision API格式
                    if (m.Images != null && m.Images.Count > 0)
                    {
                        var contentArray = new List<object>();
                        contentArray.Add(new { type = "text", text = m.Content?.ToString() ?? "" });
                        foreach (var img in m.Images)
                        {
                            contentArray.Add(new { type = "image_url", image_url = new { url = img } });
                        }
                        return new { role = m.Role, content = (object)contentArray };
                    }
                    return new { role = m.Role, content = m.Content };
                }).ToList(),
                ["temperature"] = _config.Temperature,
                ["max_tokens"] = _config.MaxTokens,
                ["stream"] = false
            };
            
            // GLM-4.5V需要enable_thinking参数
            if (_config.ModelName.Equals("zai-org/GLM-4.5V", StringComparison.OrdinalIgnoreCase))
            {
                requestBody["enable_thinking"] = true;
            }

            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8,
                "application/json");

            var endpoint = _config.ApiEndpoint.TrimEnd('/') + "/chat/completions";
            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            
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
            var messageContent = json["choices"]?[0]?["message"]?["content"]?.ToString();

            return messageContent ?? string.Empty;
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
            _httpClient?.Dispose();
        }
    }
}
