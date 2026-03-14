#nullable enable

using System;
using System.IO;
using OSGeo.GDAL;
using OSGeo.OSR;
using System.Runtime.InteropServices;
using System.Threading;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure
{
    internal static class HistoricalGdalEnvironment
    {
        private static int _registered;

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint directoryFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddDllDirectory(string path);

        private const uint DllSearchFlags = 0x00001000;

        public static void Register(int maxCache = 1024 * 1024 * 300)
        {
            if (Interlocked.CompareExchange(ref _registered, 1, 0) != 0)
            {
                return;
            }

            var assemblyDirectory = Path.GetDirectoryName(typeof(HistoricalGdalEnvironment).Assembly.Location) ?? string.Empty;
            var baseDirectory = AppContext.BaseDirectory;
            var gdalRoot = ResolveGdalRoot(assemblyDirectory, baseDirectory);
            var nativeDirectory = Path.Combine(gdalRoot, Environment.Is64BitProcess ? "x64" : "x86");
            var pluginDirectory = Path.Combine(nativeDirectory, "plugins");
            var gdalData = Path.Combine(gdalRoot, "data");
            var projShare = Path.Combine(gdalRoot, "share");
            var certificateFile = Path.Combine(gdalRoot, "curl-ca-bundle.crt");

            if (!Directory.Exists(nativeDirectory))
            {
                throw new DirectoryNotFoundException($"GDAL native directory not found: {nativeDirectory}");
            }

            SetDefaultDllDirectories(DllSearchFlags);
            AddDllDirectory(nativeDirectory);
            if (Directory.Exists(pluginDirectory))
            {
                AddDllDirectory(pluginDirectory);
            }

            SetConfig("GDAL_DATA", gdalData);
            SetConfig("GDAL_DRIVER_PATH", pluginDirectory);
            SetConfig("GEOTIFF_CSV", gdalData);
            SetConfig("PROJ_LIB", projShare);
            if (File.Exists(certificateFile))
            {
                SetConfig("GDAL_CURL_CA_BUNDLE", certificateFile);
            }

            Osr.SetPROJSearchPaths([projShare]);
            Gdal.AllRegister();
            Gdal.SetCacheMax(maxCache);
        }

        private static void SetConfig(string key, string value)
        {
            Environment.SetEnvironmentVariable(key, value);
            Gdal.SetConfigOption(key, value);
        }

        private static string ResolveGdalRoot(string assemblyDirectory, string appBaseDirectory)
        {
            var assemblyCandidate = Path.Combine(assemblyDirectory, "gdal");
            if (!string.IsNullOrWhiteSpace(assemblyDirectory) && Directory.Exists(assemblyCandidate))
            {
                return assemblyCandidate;
            }

            var appBaseCandidate = Path.Combine(appBaseDirectory, "gdal");
            if (Directory.Exists(appBaseCandidate))
            {
                return appBaseCandidate;
            }

            return assemblyCandidate;
        }
    }
}
