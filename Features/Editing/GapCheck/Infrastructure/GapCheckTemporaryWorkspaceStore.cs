using System;
using System.IO;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.Editing.GapCheck.Infrastructure
{
    /// <summary>
    /// 管理缝隙检查的临时工作空间及其被占用时的清理重试。
    /// </summary>
    internal sealed class GapCheckTemporaryWorkspaceStore
    {
        internal string CreateWorkspace()
        {
            string path = Path.Combine(Path.GetTempPath(), "GapCheck_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(path);
            return path;
        }

        internal async Task CleanupAsync(string path, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            await Task.Delay(1000).ConfigureAwait(false);
            const int maxRetries = 5;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                        catch (Exception exception)
                        {
                            log?.Invoke($"删除文件失败: {Path.GetFileName(file)} - {exception.Message}");
                        }
                    }

                    Directory.Delete(path, recursive: true);
                    log?.Invoke("临时文件清理完成");
                    return;
                }
                catch (Exception exception)
                {
                    log?.Invoke($"清理临时文件失败 (尝试 {attempt}/{maxRetries}): {exception.Message}");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(2000).ConfigureAwait(false);
                    }
                }
            }

            log?.Invoke($"无法完全清理临时文件夹: {path}");
            log?.Invoke("这些文件将在系统重启后自动清理");
        }
    }
}
