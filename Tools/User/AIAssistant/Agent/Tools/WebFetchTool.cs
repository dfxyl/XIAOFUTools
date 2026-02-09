using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Tools.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// 网页抓取工具（类似 MCP fetch，支持分段读取）
    /// </summary>
    public sealed class WebFetchTool : IGISTool
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private const int MaxFetchAttempts = 3;

        public string Name => "web_fetch";

        public string Description => "抓取指定 URL 网页正文，支持 max_length 与 start_index 分段读取，并返回页面中的可继续抓取链接。";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["url"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "要抓取的网页 URL"
                },
                ["max_length"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "最多返回字符数(200-50000)",
                    ["default"] = 5000,
                    ["minimum"] = 200,
                    ["maximum"] = 50000
                },
                ["start_index"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "从第几个字符开始返回",
                    ["default"] = 0,
                    ["minimum"] = 0
                },
                ["raw"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "是否返回原始 HTML（默认 false）",
                    ["default"] = false
                }
            },
            ["required"] = new JArray { "url" }
        };

        public async Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            try
            {
                var rawUrl = parameters?["url"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(rawUrl))
                {
                    return ToolResult.CreateError("URL 不能为空");
                }

                if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return ToolResult.CreateError("URL 格式无效，仅支持 http/https");
                }

                var maxLength = parameters?["max_length"]?.ToObject<int?>() ?? 5000;
                maxLength = Math.Clamp(maxLength, 200, 50000);

                var startIndex = parameters?["start_index"]?.ToObject<int?>() ?? 0;
                startIndex = Math.Max(0, startIndex);

                var raw = parameters?["raw"]?.ToObject<bool?>() ?? false;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var fetchResult = await FetchWithFallbackAsync(uri, cts.Token);
                if (!fetchResult.Success)
                {
                    return ToolResult.CreateError(fetchResult.ErrorMessage);
                }

                var mediaType = fetchResult.MediaType ?? string.Empty;
                var body = fetchResult.Body;
                if (string.IsNullOrWhiteSpace(body))
                {
                    return ToolResult.CreateError("抓取成功但内容为空");
                }

                var links = ExtractLinks(body, uri, 30);

                var content = raw ? body : ConvertHtmlToMarkdown(body, uri);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return ToolResult.CreateError("网页正文提取失败，建议重试 raw=true");
                }

                if (startIndex >= content.Length)
                {
                    return ToolResult.CreateSuccess(
                        "网页抓取完成，已到内容末尾",
                        new
                        {
                            url = uri.ToString(),
                            content_type = mediaType,
                            fetchedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            fetched_via = fetchResult.FetchedVia,
                            attempt_count = fetchResult.AttemptCount,
                            start_index = startIndex,
                            max_length = maxLength,
                            total_length = content.Length,
                            returned_length = 0,
                            has_more = false,
                            next_start_index = -1,
                            raw,
                            links,
                            content = string.Empty
                        });
                }

                var returnedLength = Math.Min(maxLength, content.Length - startIndex);
                var chunk = content.Substring(startIndex, returnedLength);
                var nextStartIndex = startIndex + returnedLength;
                var hasMore = nextStartIndex < content.Length;

                return ToolResult.CreateSuccess(
                    $"网页抓取成功，返回 {returnedLength} 字符",
                    new
                    {
                        url = uri.ToString(),
                        content_type = mediaType,
                        fetchedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        fetched_via = fetchResult.FetchedVia,
                        attempt_count = fetchResult.AttemptCount,
                        start_index = startIndex,
                        max_length = maxLength,
                        total_length = content.Length,
                        returned_length = returnedLength,
                        has_more = hasMore,
                        next_start_index = hasMore ? nextStartIndex : -1,
                        raw,
                        links,
                        content = chunk
                    });
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError($"网页抓取失败: {ex.Message}");
            }
        }

        private static async Task<FetchResult> FetchWithFallbackAsync(Uri uri, CancellationToken cancellationToken)
        {
            var directResult = await FetchWithRetryAsync(uri, cancellationToken, "direct");
            if (directResult.Success)
            {
                return directResult;
            }

            // 5xx/403/429 常见于站点网关或反爬，降级尝试文本镜像抓取。
            if (directResult.StatusCode == 403 || directResult.StatusCode == 429 || directResult.StatusCode >= 500)
            {
                var mirrorUrl = BuildJinaMirrorUri(uri);
                if (mirrorUrl != null)
                {
                    var mirrorResult = await FetchWithRetryAsync(mirrorUrl, cancellationToken, "jina_mirror");
                    if (mirrorResult.Success)
                    {
                        return mirrorResult;
                    }

                    return mirrorResult;
                }
            }

            return directResult;
        }

        private static async Task<FetchResult> FetchWithRetryAsync(Uri uri, CancellationToken cancellationToken, string fetchedVia)
        {
            string lastError = null;
            var lastStatus = 0;

            for (var attempt = 1; attempt <= MaxFetchAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                    request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
                    request.Headers.TryAddWithoutValidation("Pragma", "no-cache");

                    using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
                    var statusCode = (int)response.StatusCode;

                    if (response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        return FetchResult.SuccessResult(
                            body,
                            response.Content.Headers.ContentType?.MediaType,
                            fetchedVia,
                            attempt);
                    }

                    lastStatus = statusCode;
                    lastError = $"Response status code does not indicate success: {statusCode} ({response.ReasonPhrase}).";

                    if (!IsRetriableStatus(statusCode) || attempt == MaxFetchAttempts)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    if (attempt == MaxFetchAttempts)
                    {
                        break;
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(350 * attempt), cancellationToken);
            }

            var errorMessage = lastStatus > 0
                ? $"网页抓取失败: Response status code does not indicate success: {lastStatus}."
                : $"网页抓取失败: {lastError ?? "未知网络错误"}";

            return FetchResult.FailureResult(errorMessage, lastStatus, fetchedVia, MaxFetchAttempts);
        }

        private static bool IsRetriableStatus(int statusCode)
        {
            return statusCode == 408 || statusCode == 425 || statusCode == 429 || statusCode == 500 || statusCode == 502 || statusCode == 503 || statusCode == 504;
        }

        private static Uri BuildJinaMirrorUri(Uri uri)
        {
            if (uri == null)
            {
                return null;
            }

            var raw = uri.ToString();
            if (!raw.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var normalized = raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? "http://" + raw.Substring("https://".Length)
                : raw;

            var mirror = "https://r.jina.ai/" + normalized;
            return Uri.TryCreate(mirror, UriKind.Absolute, out var mirrorUri) ? mirrorUri : null;
        }

        private static string ConvertHtmlToMarkdown(string html, Uri source)
        {
            var text = html ?? string.Empty;

            // 去掉不需要的节点，减少噪声
            text = Regex.Replace(text, "<script[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<noscript[\\s\\S]*?</noscript>", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<!--([\\s\\S]*?)-->", " ", RegexOptions.IgnoreCase);

            var title = ExtractTitle(text);

            text = Regex.Replace(text, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "</p>", "\n\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "</div>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "</h[1-6]>", "\n\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<li[^>]*>", "\n- ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "</li>", "\n", RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                "<a[^>]*href\\s*=\\s*\"(?<href>[^\"]+)\"[^>]*>(?<label>[\\s\\S]*?)</a>",
                match => BuildMarkdownLink(match, source),
                RegexOptions.IgnoreCase);

            text = Regex.Replace(text, "<a[^>]*href\\s*=\\s*'(?<href>[^']+)'[^>]*>(?<label>[\\s\\S]*?)</a>",
                match => BuildMarkdownLink(match, source),
                RegexOptions.IgnoreCase);

            text = Regex.Replace(text, "<[^>]+>", " ", RegexOptions.IgnoreCase);
            text = WebUtility.HtmlDecode(text);

            var lines = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => Regex.Replace(line, "\\s+", " ").Trim())
                .ToList();

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(title))
            {
                sb.AppendLine("# " + title);
                sb.AppendLine();
            }

            var emptyCount = 0;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    emptyCount++;
                    if (emptyCount <= 1)
                    {
                        sb.AppendLine();
                    }
                    continue;
                }

                emptyCount = 0;
                sb.AppendLine(line);
            }

            var result = sb.ToString().Trim();
            if (result.Length == 0)
            {
                return string.Empty;
            }

            return result;
        }

        private static string BuildMarkdownLink(Match match, Uri source)
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value ?? string.Empty).Trim();
            var label = StripHtml(match.Groups["label"].Value);
            if (string.IsNullOrWhiteSpace(href))
            {
                return label ?? string.Empty;
            }

            if (Uri.TryCreate(source, href, out var resolved))
            {
                href = resolved.ToString();
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                label = href;
            }

            return $"[{label}]({href})";
        }

        private static string ExtractTitle(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var match = Regex.Match(html, "<title[^>]*>(?<t>[\\s\\S]*?)</title>", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return StripHtml(match.Groups["t"].Value);
        }

        private static List<string> ExtractLinks(string html, Uri source, int maxLinks)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(html))
            {
                return result;
            }

            var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matches = Regex.Matches(html, "<a[^>]*href\\s*=\\s*(\"(?<u1>[^\"]+)\"|'(?<u2>[^']+)')", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var href = match.Groups["u1"].Success ? match.Groups["u1"].Value : match.Groups["u2"].Value;
                href = WebUtility.HtmlDecode(href ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(href) || href.StartsWith("#") || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!Uri.TryCreate(source, href, out var resolved))
                {
                    continue;
                }

                if (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps)
                {
                    continue;
                }

                var normalized = resolved.ToString();
                if (!dedupe.Add(normalized))
                {
                    continue;
                }

                result.Add(normalized);
                if (result.Count >= maxLinks)
                {
                    break;
                }
            }

            return result;
        }

        private static string StripHtml(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var noTags = Regex.Replace(text, "<[^>]+>", " ", RegexOptions.IgnoreCase);
            noTags = WebUtility.HtmlDecode(noTags);
            noTags = Regex.Replace(noTags, "\\s+", " ").Trim();
            return noTags;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };

            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            return client;
        }

        private sealed class FetchResult
        {
            public bool Success { get; private set; }
            public string Body { get; private set; }
            public string MediaType { get; private set; }
            public string ErrorMessage { get; private set; }
            public int StatusCode { get; private set; }
            public string FetchedVia { get; private set; }
            public int AttemptCount { get; private set; }

            public static FetchResult SuccessResult(string body, string mediaType, string fetchedVia, int attemptCount)
            {
                return new FetchResult
                {
                    Success = true,
                    Body = body,
                    MediaType = mediaType,
                    FetchedVia = fetchedVia,
                    AttemptCount = attemptCount
                };
            }

            public static FetchResult FailureResult(string errorMessage, int statusCode, string fetchedVia, int attemptCount)
            {
                return new FetchResult
                {
                    Success = false,
                    ErrorMessage = errorMessage,
                    StatusCode = statusCode,
                    FetchedVia = fetchedVia,
                    AttemptCount = attemptCount
                };
            }
        }
    }
}
