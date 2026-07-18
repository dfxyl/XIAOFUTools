using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.Conversion.TxtToFeature.Core;

namespace XIAOFUTools.Features.Conversion.TxtToFeature.Infrastructure
{
    internal sealed record TxtPlotFileReadResult(
        long FileLength,
        string EncodingDescription,
        int LineCount,
        IReadOnlyList<PlotData> Plots);

    internal sealed class TxtPlotFileReader
    {
        public TxtPlotFileReader()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public Task<TxtPlotFileReadResult> ReadAsync(
            string filePath,
            string fieldNames,
            bool swapXy,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    throw new FileNotFoundException("TXT 文件不存在。", filePath);
                }

                var bytes = File.ReadAllBytes(filePath);
                cancellationToken.ThrowIfCancellationRequested();
                var detected = DetectEncoding(bytes);
                var lines = File.ReadAllLines(filePath, detected.Encoding);
                cancellationToken.ThrowIfCancellationRequested();
                var plots = TxtPlotParser.Parse(lines, fieldNames, swapXy).ToArray();

                return new TxtPlotFileReadResult(
                    fileInfo.Length,
                    detected.Description,
                    lines.Length,
                    plots);
            }, cancellationToken);
        }

        private static DetectedEncoding DetectEncoding(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return new DetectedEncoding(Encoding.UTF8, "文件为空，使用 UTF-8 编码");
            }

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return new DetectedEncoding(Encoding.UTF8, "检测到 UTF-8 BOM");
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return new DetectedEncoding(Encoding.Unicode, "检测到 UTF-16LE BOM");
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return new DetectedEncoding(Encoding.BigEndianUnicode, "检测到 UTF-16BE BOM");
            }

            var sampleLength = Math.Min(bytes.Length, 4096);
            var sample = bytes.Take(sampleLength).ToArray();
            var candidates = new[]
            {
                new EncodingCandidate(Encoding.Default, "系统默认(ANSI)", Priority: 1),
                new EncodingCandidate(Encoding.GetEncoding("GBK"), "GBK", Priority: 2),
                new EncodingCandidate(Encoding.UTF8, "UTF-8", Priority: 3),
                new EncodingCandidate(Encoding.GetEncoding("GB2312"), "GB2312", Priority: 4),
                new EncodingCandidate(Encoding.GetEncoding("Big5"), "Big5", Priority: 5)
            };
            var best = candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Score = CalculateScore(candidate.Encoding.GetString(sample), candidate.Encoding)
                })
                .Where(item => item.Score >= 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Candidate.Priority)
                .FirstOrDefault();

            return best is null
                ? new DetectedEncoding(Encoding.Default, "无法确定文件编码，使用系统默认(ANSI)")
                : new DetectedEncoding(
                    best.Candidate.Encoding,
                    $"选择最佳编码: {best.Candidate.Name} (得分: {best.Score})");
        }

        private static int CalculateScore(string text, Encoding encoding)
        {
            if (text.Contains('\uFFFD'))
            {
                return -1;
            }

            var score = 10;
            if (encoding == Encoding.Default)
            {
                score += 25;
            }

            if (encoding.CodePage is 936 or 54936)
            {
                score += 20;
            }

            var hasChineseCharacters = text.Any(character => character is >= '\u4E00' and <= '\u9FFF');
            if (hasChineseCharacters)
            {
                score += 15;
                if (encoding.CodePage is 936 or 54936 || encoding == Encoding.Default)
                {
                    score += 10;
                }
            }

            if (encoding.CodePage == 65001)
            {
                score += 5;
            }

            if (text.All(character => character < 128) &&
                (encoding == Encoding.Default || encoding.CodePage is 936 or 54936))
            {
                score += 15;
            }

            return score;
        }

        private sealed record DetectedEncoding(Encoding Encoding, string Description);

        private sealed record EncodingCandidate(Encoding Encoding, string Name, int Priority);
    }
}
