using System;
using System.IO;
using XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb;

namespace XIAOFUTools.Tests.DataProcessing.MdbBatchToGdb
{
    public sealed class ArcGisProPythonEnvironmentTests : IDisposable
    {
        private readonly string _rootPath;

        public ArcGisProPythonEnvironmentTests()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), "XIAOFUTools.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootPath);
        }

        [Fact]
        public void TryCreate_WithValidInstallRoot_ReturnsExpectedPaths()
        {
            string installRoot = Path.Combine(_rootPath, "ArcGIS", "Pro");
            CreateFakeInstallRoot(installRoot);

            bool created = ArcGisProPythonEnvironment.TryCreate(installRoot, out ArcGisProPythonEnvironment? environment);

            Assert.True(created);
            Assert.NotNull(environment);
            Assert.Equal(Path.Combine(installRoot, "bin", "Python", "Scripts", "propy.bat"), environment!.PropyPath);
        }

        [Fact]
        public void TryCreate_WhenPropyMissing_ReturnsFalse()
        {
            string installRoot = Path.Combine(_rootPath, "ArcGIS", "Pro");
            CreateFakeInstallRoot(installRoot);
            File.Delete(Path.Combine(installRoot, "bin", "Python", "Scripts", "propy.bat"));

            bool created = ArcGisProPythonEnvironment.TryCreate(installRoot, out ArcGisProPythonEnvironment? environment);

            Assert.False(created);
            Assert.Null(environment);
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, true);
            }
        }

        private static void CreateFakeInstallRoot(string installRoot)
        {
            Directory.CreateDirectory(Path.Combine(installRoot, "bin", "Python", "Scripts"));

            File.WriteAllText(Path.Combine(installRoot, "bin", "Python", "Scripts", "propy.bat"), "@echo off");
        }
    }
}
