using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.Conversion.ExportToKml.Infrastructure
{
    /// <summary>
    /// 负责 KML/KMZ 导出过程中的本地文件与压缩包读写。
    /// ArcGIS 图层和地理处理仍由调用方在正确的 ArcGIS 任务上下文中处理。
    /// </summary>
    internal sealed class KmlArchiveFileStore
    {
        internal bool DirectoryExists(string folderPath) =>
            !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);

        internal Task EnsureOutputDirectoryAsync(string folderPath, CancellationToken cancellationToken) =>
            Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    throw new ArgumentException("输出文件夹不能为空。", nameof(folderPath));
                }

                Directory.CreateDirectory(folderPath);
            }, cancellationToken);

        internal string CreateTemporaryKmzPath() =>
            Path.Combine(Path.GetTempPath(), $"xft_export_{Guid.NewGuid():N}.kmz");

        internal Task ExtractDocKmlAsync(string kmzPath, string kmlPath, CancellationToken cancellationToken) =>
            Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var archive = ZipFile.OpenRead(kmzPath);
                var docEntry = archive.GetEntry("doc.kml")
                    ?? throw new InvalidOperationException("KMZ中未找到doc.kml");
                using var kmlStream = docEntry.Open();
                using var outputStream = new FileStream(kmlPath, FileMode.Create, FileAccess.Write, FileShare.None);
                kmlStream.CopyTo(outputStream);
            }, cancellationToken);

        internal Task<string> ReadDocKmlAsync(string kmzPath, CancellationToken cancellationToken) =>
            Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var archive = ZipFile.OpenRead(kmzPath);
                var docEntry = archive.GetEntry("doc.kml")
                    ?? throw new InvalidOperationException($"KMZ中未找到doc.kml: {Path.GetFileName(kmzPath)}");
                using var stream = docEntry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return reader.ReadToEnd();
            }, cancellationToken);

        internal Task WriteDocKmlAsync(string kmzPath, string kmlContent, CancellationToken cancellationToken) =>
            Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var archive = ZipFile.Open(kmzPath, ZipArchiveMode.Update);
                archive.GetEntry("doc.kml")?.Delete();
                var entry = archive.CreateEntry("doc.kml");
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(kmlContent ?? string.Empty);
            }, cancellationToken);

        internal Task TryDeleteFileAsync(string filePath) =>
            Task.Run(() =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch
                {
                    // 临时文件清理失败不影响主流程。
                }
            });
    }
}
