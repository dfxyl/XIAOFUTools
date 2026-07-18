using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Services
{
    public static class MfcUtility
    {
        public static string SanitizeFileName(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            return string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        }

        public static async Task<bool> GenerateMfcFileAsync(
            string sourceDataFolder,
            string outputMfcFilePath,
            string addinExecutingPath,
            Action<string>? logAction = null,
            CancellationToken cancellationToken = default)
        {
            logAction ??= Console.WriteLine;
            try
            {
                var assemblyLocation = Path.Combine(
                    Path.GetFullPath(addinExecutingPath),
                    "XIAOFUTools.dll");
                await using var inspector = OvertureMfcDuckDbInspector.CreateDefault(
                    assemblyLocation,
                    logAction);
                var generator = new OvertureMfcGenerator(
                    inspector,
                    new OvertureMfcJsonWriter());
                return await generator.GenerateAsync(
                    sourceDataFolder,
                    outputMfcFilePath,
                    logAction,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logAction("MFC 创建已取消。");
                return false;
            }
            catch (Exception exception)
            {
                logAction(
                    $"生成 MFC 文件时出错: {exception.Message}\n{exception.StackTrace}");
                return false;
            }
        }
    }
}
