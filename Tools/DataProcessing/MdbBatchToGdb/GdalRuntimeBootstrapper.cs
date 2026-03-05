using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OSGeo.GDAL;
using OSGeo.OGR;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal static class GdalRuntimeBootstrapper
    {
        private static bool _runtimeConfigured;
        private static readonly object RuntimeLock = new object();

        private static bool _ogrInitialized;
        private static readonly object OgrLock = new object();

        public static string NativeDirectory { get; private set; } = string.Empty;

        public static void EnsureInitialized()
        {
            if (_ogrInitialized)
            {
                return;
            }

            lock (OgrLock)
            {
                if (_ogrInitialized)
                {
                    return;
                }

                EnsureRuntimeConfigured();

                try
                {
                    Gdal.AllRegister();
                    Ogr.RegisterAll();
                    _ogrInitialized = true;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"GDAL 初始化失败。Native目录: {NativeDirectory}。请检查 gdal_wrap.dll 及其依赖。",
                        ex);
                }
            }
        }

        private static void EnsureRuntimeConfigured()
        {
            if (_runtimeConfigured)
            {
                return;
            }

            lock (RuntimeLock)
            {
                if (_runtimeConfigured)
                {
                    return;
                }

                string gdalRoot = ResolveGdalRootDirectory();
                string architecture = IntPtr.Size == 8 ? "x64" : "x86";
                string nativeDirectory = Path.Combine(gdalRoot, architecture);
                if (!Directory.Exists(nativeDirectory))
                {
                    throw new DirectoryNotFoundException(
                        $"GDAL native目录不存在: {nativeDirectory}。请确认 AddIn 中包含 gdal/{architecture} 或已安装 GDAL.Native 包。");
                }

                NativeDirectory = nativeDirectory;
                PrependProcessPath(nativeDirectory);

                string gdalData = Path.Combine(gdalRoot, "data");
                if (Directory.Exists(gdalData))
                {
                    Environment.SetEnvironmentVariable("GDAL_DATA", gdalData, EnvironmentVariableTarget.Process);
                    Gdal.SetConfigOption("GDAL_DATA", gdalData);
                }

                string projLib = Path.Combine(gdalRoot, "share");
                if (Directory.Exists(projLib))
                {
                    Environment.SetEnvironmentVariable("PROJ_LIB", projLib, EnvironmentVariableTarget.Process);
                    Gdal.SetConfigOption("PROJ_LIB", projLib);
                }

                _runtimeConfigured = true;
            }
        }

        private static string ResolveGdalRootDirectory()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(GdalRuntimeBootstrapper).Assembly.Location)
                                     ?? AppDomain.CurrentDomain.BaseDirectory;

            var candidates = new List<string>
            {
                Path.Combine(assemblyDirectory, "gdal")
            };

            string parent = Directory.GetParent(assemblyDirectory)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent))
            {
                candidates.Add(Path.Combine(parent, "gdal"));
            }

            string nugetRoot = FindNuGetGdalRoot();
            if (!string.IsNullOrWhiteSpace(nugetRoot))
            {
                candidates.Add(nugetRoot);
            }

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new DirectoryNotFoundException("未找到 GDAL 运行时目录。请确认 AddIn 包含 gdal 目录或已安装 GDAL.Native。");
        }

        private static string FindNuGetGdalRoot()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(userProfile))
            {
                return string.Empty;
            }

            string packageRoot = Path.Combine(userProfile, ".nuget", "packages", "gdal.native");
            if (!Directory.Exists(packageRoot))
            {
                return string.Empty;
            }

            foreach (string versionDirectory in Directory.EnumerateDirectories(packageRoot)
                         .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string candidate = Path.Combine(versionDirectory, "build", "gdal");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static void PrependProcessPath(string directory)
        {
            string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? string.Empty;
            string normalizedDirectory = Path.GetFullPath(directory).TrimEnd('\\');

            foreach (string part in currentPath.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                string normalizedPart;
                try
                {
                    normalizedPart = Path.GetFullPath(part).TrimEnd('\\');
                }
                catch
                {
                    continue;
                }

                if (string.Equals(normalizedPart, normalizedDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            string updatedPath = string.IsNullOrEmpty(currentPath)
                ? normalizedDirectory
                : normalizedDirectory + ";" + currentPath;

            Environment.SetEnvironmentVariable("PATH", updatedPath, EnvironmentVariableTarget.Process);
        }
    }
}
