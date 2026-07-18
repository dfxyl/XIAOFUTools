using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Security.Principal;
using Microsoft.Win32;

namespace XIAOFUTools.Features.User.PluginUpdate
{
    /// <summary>
    /// 更新安装器辅助类
    /// </summary>
    public static class UpdateInstaller
    {
        /// <summary>
        /// 获取ArcGIS Pro安装路径
        /// </summary>
        /// <returns>ArcGIS Pro安装路径</returns>
        public static string GetArcGISProInstallPath()
        {
            try
            {
                // 尝试从注册表获取ArcGIS Pro安装路径
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\ESRI\ArcGISPro"))
                {
                    if (key != null)
                    {
                        var installDir = key.GetValue("InstallDir") as string;
                        if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                        {
                            return installDir;
                        }
                    }
                }

                // 备用方法：检查常见安装路径
                var commonPaths = new[]
                {
                    @"C:\Program Files\ArcGIS\Pro",
                    @"C:\Program Files (x86)\ArcGIS\Pro",
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\ArcGIS\Pro"
                };

                foreach (var path in commonPaths)
                {
                    if (Directory.Exists(path))
                    {
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取ArcGIS Pro安装路径失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 获取用户插件安装目录
        /// </summary>
        /// <returns>用户插件目录路径</returns>
        public static string GetUserAddInsPath()
        {
            try
            {
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var addInsPath = Path.Combine(documentsPath, "ArcGIS", "AddIns", "ArcGISPro");
                
                if (!Directory.Exists(addInsPath))
                {
                    Directory.CreateDirectory(addInsPath);
                }
                
                return addInsPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取用户插件目录失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查是否有管理员权限
        /// </summary>
        /// <returns>是否有管理员权限</returns>
        public static bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }



        /// <summary>
        /// 使用RegisterAddIn.exe执行静默安装
        /// </summary>
        /// <param name="updateFilePath">更新文件路径</param>
        /// <returns>是否安装成功</returns>
        public static async Task<bool> SilentInstallAsync(string updateFilePath)
        {
            try
            {
                // 获取RegisterAddIn.exe路径
                var registerAddInPath = GetRegisterAddInExePath();
                if (string.IsNullOrEmpty(registerAddInPath))
                {
                    System.Diagnostics.Debug.WriteLine("未找到RegisterAddIn.exe");
                    return false;
                }

                // 使用RegisterAddIn.exe安装插件
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = registerAddInPath,
                    Arguments = $"\"{updateFilePath}\" /s",  // /s参数表示静默安装
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();

                        // 检查退出代码
                        if (process.ExitCode == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"插件安装成功: {updateFilePath}");
                            return true;
                        }
                        else
                        {
                            var error = await process.StandardError.ReadToEndAsync();
                            System.Diagnostics.Debug.WriteLine($"插件安装失败，退出代码: {process.ExitCode}, 错误: {error}");
                            return false;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"静默安装失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取RegisterAddIn.exe的路径
        /// </summary>
        /// <returns>RegisterAddIn.exe路径</returns>
        public static string GetRegisterAddInExePath()
        {
            try
            {
                // 方法1：从注册表查找ArcGIS Pro安装路径
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Esri\ArcGISPro"))
                {
                    if (key != null)
                    {
                        var installDir = key.GetValue("InstallDir") as string;
                        if (!string.IsNullOrEmpty(installDir))
                        {
                            var possiblePath = Path.Combine(installDir, "bin", "RegisterAddIn.exe");
                            if (File.Exists(possiblePath))
                            {
                                return possiblePath;
                            }
                        }
                    }
                }

                // 方法2：检查默认路径
                var defaultPath = @"C:\ProgramData\EsriProCommon\RegisterAddIn.exe";
                if (File.Exists(defaultPath))
                {
                    return defaultPath;
                }

                // 方法3：检查其他可能的路径
                var commonPaths = new[]
                {
                    @"C:\Program Files\ArcGIS\Pro\bin\RegisterAddIn.exe",
                    @"C:\Program Files (x86)\ArcGIS\Pro\bin\RegisterAddIn.exe"
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取RegisterAddIn.exe路径失败: {ex.Message}");
            }

            return null;
        }



        /// <summary>
        /// 验证更新文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否为有效的插件文件</returns>
        public static bool ValidateUpdateFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                
                // 检查文件大小（至少应该有几KB）
                if (fileInfo.Length < 1024)
                {
                    return false;
                }

                // 检查文件扩展名
                var extension = Path.GetExtension(filePath).ToLower();
                if (extension != ".esriaddinx" && extension != ".zip")
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"验证更新文件失败: {ex.Message}");
                return false;
            }
        }
    }
}
