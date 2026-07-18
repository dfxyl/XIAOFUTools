using System;
using System.IO;

namespace XIAOFUTools.Features.User.AIAssistant.Infrastructure
{
    /// <summary>
    /// 解析随 Add-in 发布的 AI 助手 Web 界面资源路径，并兼容旧版目录布局。
    /// </summary>
    internal sealed class AIAssistantUiPathResolver
    {
        internal string ResolveChatHtmlPath(string assemblyLocation)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(assemblyLocation);
            string assemblyDirectory = Path.GetDirectoryName(assemblyLocation)
                ?? throw new InvalidOperationException("无法获取程序集目录。");
            string[] candidates =
            {
                Path.Combine(assemblyDirectory, "Features", "User", "AIAssistant", "UI", "chat.html"),
                Path.Combine(assemblyDirectory, "Tools", "User", "AIAssistant", "UI", "chat.html")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            throw new FileNotFoundException(
                $"AI助手界面文件未找到。已检查: {string.Join("; ", candidates)}",
                candidates[0]);
        }
    }
}
