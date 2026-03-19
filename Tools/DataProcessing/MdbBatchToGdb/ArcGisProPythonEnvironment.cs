using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal sealed class ArcGisProPythonEnvironment
    {
        private const string ScriptRelativePath = "scripts\\mdb_to_gdb_arcgispro.py";

        public string InstallRoot { get; private init; } = string.Empty;

        public string PropyPath { get; private init; } = string.Empty;

        public string ScriptPath { get; private init; } = string.Empty;

        public static ArcGisProPythonEnvironment Resolve()
        {
            foreach (string installRoot in EnumerateInstallRoots())
            {
                if (TryCreate(installRoot, out ArcGisProPythonEnvironment? environment))
                {
                    return environment!;
                }
            }

            throw new DirectoryNotFoundException(
                "未找到可用的 ArcGIS Pro 自带 Python 环境。请确认 ArcGIS Pro 已安装，且输出目录包含 mdb_to_gdb_arcgispro.py。");
        }

        public static bool TryCreate(string installRoot, out ArcGisProPythonEnvironment? environment)
        {
            environment = null;
            if (string.IsNullOrWhiteSpace(installRoot))
            {
                return false;
            }

            string normalizedRoot = NormalizeRootPath(installRoot);
            if (!Directory.Exists(normalizedRoot))
            {
                return false;
            }

            string propyPath = Path.Combine(normalizedRoot, "bin", "Python", "Scripts", "propy.bat");
            string scriptPath = ResolveBundledScriptPath();

            if (!File.Exists(propyPath) || !File.Exists(scriptPath))
            {
                return false;
            }

            environment = new ArcGisProPythonEnvironment
            {
                InstallRoot = normalizedRoot,
                PropyPath = propyPath,
                ScriptPath = scriptPath
            };

            return true;
        }

        public static string ResolveBundledScriptPath()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(ArcGisProPythonEnvironment).Assembly.Location)
                                     ?? AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(assemblyDirectory, ScriptRelativePath),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ScriptRelativePath),
                Path.Combine(Directory.GetParent(assemblyDirectory)?.FullName ?? string.Empty, ScriptRelativePath)
            };

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return Path.GetFullPath(Path.Combine(assemblyDirectory, ScriptRelativePath));
        }

        private static IEnumerable<string> EnumerateInstallRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in EnumerateRegistryInstallRoots())
            {
                if (!string.IsNullOrWhiteSpace(root) && seen.Add(NormalizeRootPath(root)))
                {
                    yield return NormalizeRootPath(root);
                }
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string x86ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            foreach (string candidate in new[]
            {
                Path.Combine(programFiles, "ArcGIS", "Pro"),
                Path.Combine(x86ProgramFiles, "ArcGIS", "Pro"),
                @"D:\RUANJIAN\ArcGIS\Pro"
            })
            {
                if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(NormalizeRootPath(candidate)))
                {
                    yield return NormalizeRootPath(candidate);
                }
            }
        }

        private static IEnumerable<string> EnumerateRegistryInstallRoots()
        {
            foreach (RegistryKey baseKey in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                foreach (string subKey in new[]
                {
                    @"SOFTWARE\ESRI\ArcGISPro",
                    @"SOFTWARE\WOW6432Node\ESRI\ArcGISPro"
                })
                {
                    using RegistryKey? key = baseKey.OpenSubKey(subKey);
                    string? installDir = key?.GetValue("InstallDir") as string;
                    if (!string.IsNullOrWhiteSpace(installDir))
                    {
                        yield return installDir;
                    }
                }
            }
        }

        private static string NormalizeRootPath(string value)
        {
            return Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
