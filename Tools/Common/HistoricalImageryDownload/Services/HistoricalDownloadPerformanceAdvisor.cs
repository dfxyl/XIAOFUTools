#nullable enable

using System;
using System.Collections.Generic;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    internal sealed class HistoricalDownloadPerformanceEvaluation
    {
        public bool ShouldWarn { get; init; }

        public bool ShouldBlock { get; init; }

        public bool UseFastClip { get; init; }

        public int RecommendedTileConcurrency { get; init; }

        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    }

    internal static class HistoricalDownloadPerformanceAdvisor
    {
        private const int WarningTileCount = 800;
        private const int WarningTaskCount = 1500;
        private const int BlockingTileCount = 6000;
        private const int BlockingTaskCount = 12000;
        private const int FastClipTileCount = 256;

        public static int RecommendTileConcurrency(int cpuCount, int tileCount, bool preciseClip)
        {
            var normalizedCpu = Math.Max(1, cpuCount);
            var upperBound = preciseClip ? 8 : 24;
            var lowerBound = preciseClip ? 2 : 4;
            var baseline = preciseClip ? normalizedCpu : normalizedCpu * 2;
            return Math.Clamp(Math.Min(tileCount, baseline), lowerBound, upperBound);
        }

        public static HistoricalDownloadPerformanceEvaluation Evaluate(int tilesPerVersion, int versionCount, bool preciseClip)
        {
            var messages = new List<string>();
            var totalTasks = Math.Max(1, tilesPerVersion) * Math.Max(1, versionCount);
            var useFastClip = preciseClip && tilesPerVersion >= FastClipTileCount;
            var exceedsWarningThreshold = tilesPerVersion >= WarningTileCount || totalTasks >= WarningTaskCount;
            var exceedsBlockingThreshold = tilesPerVersion >= BlockingTileCount || totalTasks >= BlockingTaskCount;
            var shouldWarn = exceedsWarningThreshold || exceedsBlockingThreshold || useFastClip;

            if (exceedsBlockingThreshold)
            {
                messages.Add($"下载范围过大：单版本约 {tilesPerVersion} 个瓦片，总任务量约 {totalTasks}。请缩小范围、降低级别或减少版本数后再下载。");
            }
            else if (shouldWarn)
            {
                messages.Add($"下载任务较大：单版本约 {tilesPerVersion} 个瓦片，总任务量约 {totalTasks}。");
            }

            if (useFastClip)
            {
                messages.Add("当前范围较大，将自动使用快速裁切模式以提升下载速度。");
            }

            return new HistoricalDownloadPerformanceEvaluation
            {
                ShouldWarn = shouldWarn,
                ShouldBlock = false,
                UseFastClip = useFastClip,
                RecommendedTileConcurrency = RecommendTileConcurrency(Environment.ProcessorCount, tilesPerVersion, preciseClip),
                Messages = messages
            };
        }
    }
}
