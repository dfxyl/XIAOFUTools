using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Linq;
using XIAOFUTools.Tools.Settings;
using XIAOFUTools.Common;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Globalization;

namespace XIAOFUTools.Tools.PluginUpdate
{
    /// <summary>
    /// 插件更新检查器
    /// </summary>
    public static class UpdateChecker
    {
        private const string VersionUrl = "https://gitee.com/XFTools/xiaofutools/raw/master/version.json";

        private static async Task<JObject> GetVersionJsonAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var response = await client.GetStringAsync(VersionUrl);
                    return JObject.Parse(response);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取版本JSON失败: {ex.Message}");
                return null;
            }
        }

        internal static string GetCurrentDesktopVersion()
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return null;

                var vi = FileVersionInfo.GetVersionInfo(exePath);
                if (vi == null)
                    return null;

                // 优先用 FileMajorPart 等数字字段，避免 ProductVersion 带有额外字符串
                if (vi.FileMajorPart > 0)
                {
                    return $"{vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart}.{vi.FilePrivatePart}";
                }

                return vi.FileVersion;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取ArcGIS Pro版本失败: {ex.Message}");
                return null;
            }
        }

        private static bool IsDesktopVersionCompatible(string currentDesktopVersion, string minDesktopVersion)
        {
            if (string.IsNullOrWhiteSpace(minDesktopVersion))
                return true;
            if (string.IsNullOrWhiteSpace(currentDesktopVersion))
                return true;

            return CompareVersions(currentDesktopVersion, minDesktopVersion) >= 0;
        }

        /// <summary>
        /// 获取最新版本信息
        /// </summary>
        /// <returns>最新版本号</returns>
        public static async Task<string> GetLatestVersionAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"正在从 {VersionUrl} 获取版本信息...");
                var json = await GetVersionJsonAsync();
                var version = json?["version"]?.ToString();
                System.Diagnostics.Debug.WriteLine($"解析到的版本号: {version}");
                return version;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取最新版本失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取下载链接
        /// </summary>
        /// <returns>下载链接</returns>
        public static async Task<string> GetDownloadUrlAsync()
        {
            try
            {
                var json = await GetVersionJsonAsync();
                return json?["downloadUrl"]?.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取下载链接失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取更新说明（使用您指定的changelog格式）
        /// </summary>
        /// <returns>更新说明</returns>
        public static async Task<string> GetUpdateNotesAsync()
        {
            try
            {
                var json = await GetVersionJsonAsync();
                if (json == null) return "获取更新说明失败";

                // 使用您指定的changelog格式
                var changelog = json["changelog"] as JArray;
                if (changelog != null && changelog.Count > 0)
                {
                    var changelogText = string.Join("\n• ", changelog.Select(item => item.ToString()));
                    return "• " + changelogText;
                }

                return "暂无更新说明";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取更新说明失败: {ex.Message}");
                return "获取更新说明失败";
            }
        }

        /// <summary>
        /// 获取发布日期
        /// </summary>
        /// <returns>发布日期</returns>
        public static async Task<string> GetReleaseDateAsync()
        {
            try
            {
                var json = await GetVersionJsonAsync();
                return json?["releaseDate"]?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取发布日期失败: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取通知信息
        /// </summary>
        /// <returns>通知信息</returns>
        public static async Task<string> GetNoticeAsync()
        {
            try
            {
                var json = await GetVersionJsonAsync();
                return json?["notice"]?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取通知信息失败: {ex.Message}");
                return "";
            }
        }

        private static async Task<string> GetMinDesktopVersionAsync()
        {
            try
            {
                var json = await GetVersionJsonAsync();
                return json?["minDesktopVersion"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取当前版本号
        /// </summary>
        /// <returns>当前版本号</returns>
        public static string GetCurrentVersion()
        {
            // 使用手动指定的版本号
            return XIAOFUTools.Common.VersionInfo.CurrentVersion;
        }

        /// <summary>
        /// 检查是否有新版本可用
        /// </summary>
        /// <param name="newVersion">新版本号</param>
        /// <returns>是否有新版本</returns>
        public static bool IsNewVersionAvailable(string newVersion)
        {
            if (string.IsNullOrEmpty(newVersion))
            {
                return false;
            }

            try
            {
                var currentVersion = GetCurrentVersion();
                return CompareVersions(newVersion, currentVersion) > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"版本比较失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 比较两个版本号
        /// </summary>
        /// <param name="version1">版本1</param>
        /// <param name="version2">版本2</param>
        /// <returns>比较结果：1表示version1更新，-1表示version2更新，0表示相同</returns>
        private static int CompareVersions(string version1, string version2)
        {
            if (string.IsNullOrEmpty(version1) || string.IsNullOrEmpty(version2))
            {
                return 0;
            }

            var v1Parts = version1.Split('.');
            var v2Parts = version2.Split('.');
            
            int maxLength = Math.Max(v1Parts.Length, v2Parts.Length);
            
            for (int i = 0; i < maxLength; i++)
            {
                int v1Part = 0;
                int v2Part = 0;
                
                if (i < v1Parts.Length && int.TryParse(v1Parts[i], out int v1Parsed))
                {
                    v1Part = v1Parsed;
                }
                
                if (i < v2Parts.Length && int.TryParse(v2Parts[i], out int v2Parsed))
                {
                    v2Part = v2Parsed;
                }
                
                if (v1Part > v2Part)
                {
                    return 1;
                }
                else if (v1Part < v2Part)
                {
                    return -1;
                }
            }
            
            return 0;
        }

        /// <summary>
        /// 检查更新（异步）
        /// </summary>
        /// <returns>更新信息</returns>
        public static async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                var json = await GetVersionJsonAsync();

                System.Diagnostics.Debug.WriteLine($"当前版本: {GetCurrentVersion()}");
                var latestVersion = json?["version"]?.ToString();
                System.Diagnostics.Debug.WriteLine($"最新版本: {latestVersion}");
                
                if (string.IsNullOrEmpty(latestVersion))
                {
                    System.Diagnostics.Debug.WriteLine("无法获取版本信息");
                    return new UpdateInfo
                    {
                        HasUpdate = false,
                        ErrorMessage = "无法获取版本信息"
                    };
                }

                var hasUpdate = IsNewVersionAvailable(latestVersion);
                System.Diagnostics.Debug.WriteLine($"是否有更新: {hasUpdate}");

                var currentDesktopVersion = GetCurrentDesktopVersion();
                var minDesktopVersion = json?["minDesktopVersion"]?.ToString();
                var isCompatible = IsDesktopVersionCompatible(currentDesktopVersion, minDesktopVersion);
                
                var updateInfo = new UpdateInfo
                {
                    HasUpdate = hasUpdate,
                    CurrentVersion = GetCurrentVersion(),
                    LatestVersion = latestVersion,
                    CurrentDesktopVersion = currentDesktopVersion,
                    MinDesktopVersion = minDesktopVersion,
                    IsDesktopVersionCompatible = isCompatible
                };

                if (hasUpdate)
                {
                    System.Diagnostics.Debug.WriteLine("获取更新详细信息...");
                    updateInfo.DownloadUrl = json?["downloadUrl"]?.ToString();
                    updateInfo.ReleaseDate = json?["releaseDate"]?.ToString() ?? "";
                    updateInfo.Notice = json?["notice"]?.ToString() ?? "";

                    var changelog = json?["changelog"] as JArray;
                    if (changelog != null && changelog.Count > 0)
                    {
                        var changelogText = string.Join("\n• ", changelog.Select(item => item.ToString()));
                        updateInfo.UpdateNotes = "• " + changelogText;
                    }
                    else
                    {
                        updateInfo.UpdateNotes = "暂无更新说明";
                    }
                }

                return updateInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查更新失败: {ex.Message}");
                return new UpdateInfo
                {
                    HasUpdate = false,
                    ErrorMessage = $"检查更新失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 检查更新（带进度条）
        /// </summary>
        /// <param name="progressSource">进度条源</param>
        /// <returns>更新信息</returns>
        public static async Task<UpdateInfo> CheckForUpdatesWithProgressAsync(CancelableProgressorSource progressSource = null)
        {
            try
            {
                // 第一步：连接服务器
                if (progressSource != null)
                {
                    progressSource.Progressor.Status = "正在连接服务器...";
                    progressSource.Progressor.Value = 10;
                }
                await Task.Delay(500); // 让用户看到这个步骤

                // 第二步：获取版本信息
                if (progressSource != null)
                {
                    progressSource.Progressor.Status = "正在获取版本信息...";
                    progressSource.Progressor.Value = 25;
                }
                await Task.Delay(300);

                var json = await GetVersionJsonAsync();
                var latestVersion = json?["version"]?.ToString();
                if (string.IsNullOrEmpty(latestVersion))
                {
                    return new UpdateInfo
                    {
                        HasUpdate = false,
                        ErrorMessage = "无法获取版本信息"
                    };
                }

                // 第三步：分析版本数据
                if (progressSource != null)
                {
                    progressSource.Progressor.Status = "正在分析版本数据...";
                    progressSource.Progressor.Value = 40;
                }
                await Task.Delay(400);

                var hasUpdate = IsNewVersionAvailable(latestVersion);
                var currentDesktopVersion = GetCurrentDesktopVersion();
                var minDesktopVersion = json?["minDesktopVersion"]?.ToString();
                var isCompatible = IsDesktopVersionCompatible(currentDesktopVersion, minDesktopVersion);

                var updateInfo = new UpdateInfo
                {
                    HasUpdate = hasUpdate,
                    CurrentVersion = GetCurrentVersion(),
                    LatestVersion = latestVersion,
                    CurrentDesktopVersion = currentDesktopVersion,
                    MinDesktopVersion = minDesktopVersion,
                    IsDesktopVersionCompatible = isCompatible
                };

                if (hasUpdate)
                {
                    // 第四步：获取下载链接
                    if (progressSource != null)
                    {
                        progressSource.Progressor.Status = "正在获取下载链接...";
                        progressSource.Progressor.Value = 55;
                    }
                    await Task.Delay(300);
                    updateInfo.DownloadUrl = await GetDownloadUrlAsync();

                    // 第五步：获取更新日志
                    if (progressSource != null)
                    {
                        progressSource.Progressor.Status = "正在获取更新日志...";
                        progressSource.Progressor.Value = 70;
                    }
                    await Task.Delay(300);
                    updateInfo.UpdateNotes = await GetUpdateNotesAsync();

                    // 第六步：获取发布信息
                    if (progressSource != null)
                    {
                        progressSource.Progressor.Status = "正在获取发布信息...";
                        progressSource.Progressor.Value = 85;
                    }
                    await Task.Delay(300);
                    updateInfo.ReleaseDate = await GetReleaseDateAsync();
                    updateInfo.Notice = await GetNoticeAsync();
                }
                else
                {
                    // 如果没有更新，也要显示一些步骤
                    if (progressSource != null)
                    {
                        progressSource.Progressor.Status = "验证当前版本...";
                        progressSource.Progressor.Value = 70;
                    }
                    await Task.Delay(400);

                    if (progressSource != null)
                    {
                        progressSource.Progressor.Status = "确认无需更新...";
                        progressSource.Progressor.Value = 90;
                    }
                    await Task.Delay(300);
                }

                // 最后一步：完成检查
                if (progressSource != null)
                {
                    progressSource.Progressor.Status = "检查完成";
                    progressSource.Progressor.Value = 100;
                }
                await Task.Delay(200);

                return updateInfo;
            }
            catch (Exception ex)
            {
                return new UpdateInfo
                {
                    HasUpdate = false,
                    ErrorMessage = $"检查更新失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 启动时检查更新（静默检查）
        /// </summary>
        public static async Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                var settings = SettingsManager.Settings.PluginUpdate;
                System.Diagnostics.Debug.WriteLine($"执行启动时检查更新 - 设置: 启动时检查={settings.CheckForUpdatesOnStartup}, 通知={settings.NotifyOnUpdate}");

                // 检查是否启用启动时检查更新
                if (!settings.CheckForUpdatesOnStartup)
                {
                    System.Diagnostics.Debug.WriteLine("启动时检查更新已禁用");
                    return;
                }

                // 执行检查（启动时检查不受时间间隔限制）
                System.Diagnostics.Debug.WriteLine("开始执行更新检查...");
                var updateInfo = await CheckForUpdatesAsync();
                System.Diagnostics.Debug.WriteLine($"检查完成 - 是否有更新: {updateInfo.HasUpdate}");

                // 更新最后检查时间
                settings.LastCheckTime = DateTime.Now;
                SettingsManager.SaveSettings();

                // 如果有更新且启用通知，只弹一次询问框
                if (updateInfo.HasUpdate && settings.NotifyOnUpdate)
                {
                    System.Diagnostics.Debug.WriteLine("发现新版本，准备显示通知");
                    var message = $"发现新版本 {updateInfo.LatestVersion}！\n";

                    if (!string.IsNullOrEmpty(updateInfo.ReleaseDate))
                    {
                        message += $"发布日期: {updateInfo.ReleaseDate}\n";
                    }

                    message += $"\n更新内容:\n{updateInfo.UpdateNotes}\n";

                    if (!string.IsNullOrEmpty(updateInfo.Notice))
                    {
                        message += $"\n{updateInfo.Notice}\n";
                    }

                    if (!updateInfo.IsDesktopVersionCompatible)
                    {
                        var currentDv = string.IsNullOrEmpty(updateInfo.CurrentDesktopVersion) ? "未知" : updateInfo.CurrentDesktopVersion;
                        var minDv = string.IsNullOrEmpty(updateInfo.MinDesktopVersion) ? "未知" : updateInfo.MinDesktopVersion;

                        message += $"\n\n当前 ArcGIS Pro: {currentDv}\n最低支持版本: {minDv}\n版本不匹配，仅显示更新信息（不提供下载）。";
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                            message,
                            "发现更新",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                        return;
                    }

                    message += "\n是否立即下载并安装？";

                    var result = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        message,
                        "发现更新",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        await DownloadAndInstallUpdateAsync(updateInfo);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("没有发现新版本或通知已禁用");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动时检查更新失败: {ex.Message}");
            }
        }



        /// <summary>
        /// 下载并安装更新
        /// </summary>
        public static async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
        {
            try
            {
                if (updateInfo != null && !updateInfo.IsDesktopVersionCompatible)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        "当前 ArcGIS Pro 版本不满足该更新要求，仅显示更新信息，不提供下载。",
                        "版本不匹配",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return false;
                }

                if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        "下载链接无效",
                        "下载失败",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return false;
                }

                // 定义临时文件路径（参考原代码的命名方式）
                var fileName = "XIAOFUTools.esriAddinX";
                var filePath = Path.Combine(Path.GetTempPath(), fileName);

                // 使用ArcGIS Pro自带的进度条下载
                var pd = new ProgressDialog("正在下载更新", "取消", 100, true);
                var cps = new CancelableProgressorSource(pd);

                await QueuedTask.Run(async () =>
                {
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                        client.Timeout = TimeSpan.FromMinutes(10); // 10分钟超时

                        var response = await client.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var readBytes = 0L;

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[8192];
                            int bytesRead;

                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                // 检查是否取消
                                if (cps.Progressor.CancellationToken.IsCancellationRequested)
                                {
                                    throw new OperationCanceledException("下载已取消");
                                }

                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                readBytes += bytesRead;

                                // 更新ArcGIS Pro进度条
                                if (totalBytes > 0)
                                {
                                    var progressValue = (uint)((readBytes * 100) / totalBytes);
                                    cps.Progressor.Value = progressValue;

                                    // 更新状态文本
                                    var downloadedMB = readBytes / 1024.0 / 1024.0;
                                    var totalMB = totalBytes / 1024.0 / 1024.0;
                                    cps.Progressor.Status = $"已下载 {downloadedMB:F1} MB / {totalMB:F1} MB";
                                }
                            }
                        }
                    }
                }, cps.Progressor);

                // 验证文件是否下载成功
                if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        "下载文件失败或文件损坏",
                        "下载失败",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return false;
                }

                // 安装更新
                return await InstallUpdateAsync(filePath);
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"下载更新失败: {ex.Message}",
                    "下载失败",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 安装更新
        /// </summary>
        private static async Task<bool> InstallUpdateAsync(string filePath)
        {
            try
            {
                // 验证更新文件
                if (!UpdateInstaller.ValidateUpdateFile(filePath))
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        "下载的更新文件无效或已损坏",
                        "文件验证失败",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return false;
                }

                // 直接使用RegisterAddIn.exe安装，不弹窗
                var installSuccess = await UpdateInstaller.SilentInstallAsync(filePath);

                // 安装完成，删除临时文件
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (Exception deleteEx)
                {
                    System.Diagnostics.Debug.WriteLine($"删除临时文件失败: {deleteEx.Message}");
                }

                if (installSuccess)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        "升级安装完成，请关闭并重新启动 ArcGIS Pro。",
                        "安装完成",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return true;
                }
                else
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        $"安装失败。您可以手动双击下载的文件进行安装：\n{filePath}",
                        "安装失败",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"安装更新失败: {ex.Message}\n\n您可以手动双击下载的文件进行安装：\n{filePath}",
                    "安装失败",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }
    }

    /// <summary>
    /// 更新信息类（按照您指定的JSON格式）
    /// </summary>
    public class UpdateInfo
    {
        /// <summary>
        /// 是否有更新
        /// </summary>
        public bool HasUpdate { get; set; }

        /// <summary>
        /// 当前版本
        /// </summary>
        public string CurrentVersion { get; set; }

        /// <summary>
        /// 最新版本
        /// </summary>
        public string LatestVersion { get; set; }

        public string CurrentDesktopVersion { get; set; }

        public string MinDesktopVersion { get; set; }

        public bool IsDesktopVersionCompatible { get; set; } = true;

        /// <summary>
        /// 发布日期
        /// </summary>
        public string ReleaseDate { get; set; }

        /// <summary>
        /// 下载链接
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// 更新日志（changelog数组格式）
        /// </summary>
        public string UpdateNotes { get; set; }

        /// <summary>
        /// 通知信息
        /// </summary>
        public string Notice { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
