using System;
using System.Collections.Generic;
using System.Globalization;

namespace XIAOFUTools.Features.Analysis.BrowseFeatures.Core
{
    internal enum BrowseScopeMode
    {
        Selection = 0,
        All = 1
    }

    internal enum BrowseSortMode
    {
        ObjectId = 0,
        Field = 1
    }

    internal enum ReviewStatus
    {
        Pending = 0,
        Passed = 1,
        Issue = 2
    }

    internal sealed class FeatureSnapshotItem
    {
        public long ObjectId { get; init; }
        public object? SortValue { get; init; }
    }

    internal sealed class ReviewRecord
    {
        public string ProjectKey { get; init; } = string.Empty;
        public string BatchId { get; init; } = string.Empty;
        public string BatchName { get; init; } = string.Empty;
        public string Reviewer { get; init; } = string.Empty;
        public ReviewStatus ReviewStatus { get; init; } = ReviewStatus.Pending;
        public string Notes { get; init; } = string.Empty;
        public string LayerName { get; init; } = string.Empty;
        public string LayerUri { get; init; } = string.Empty;
        public long SourceObjectId { get; init; }
        public string GeometryType { get; init; } = string.Empty;
        public string GeometryWkt { get; init; } = string.Empty;
        public DateTime VisitedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    internal static class BrowseFeaturesCore
    {
        public static string CreateDefaultBatchId(DateTime now)
        {
            return now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        }

        public static ReviewStatus NormalizeReviewStatus(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return ReviewStatus.Pending;
            }

            var value = raw.Trim();
            if (string.Equals(value, "未判定", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return ReviewStatus.Pending;
            }

            if (string.Equals(value, "通过", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "passed", StringComparison.OrdinalIgnoreCase))
            {
                return ReviewStatus.Passed;
            }

            if (string.Equals(value, "问题", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "issue", StringComparison.OrdinalIgnoreCase))
            {
                return ReviewStatus.Issue;
            }

            return ReviewStatus.Pending;
        }

        public static string ToDisplayText(ReviewStatus status)
        {
            return status switch
            {
                ReviewStatus.Passed => "通过",
                ReviewStatus.Issue => "问题",
                _ => "未判定"
            };
        }

        public static int CompareSortValue(object? left, object? right, bool descending)
        {
            // 约定：空值始终排在最后，不受升降序影响。
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int compareResult;
            if (left is string || right is string)
            {
                var leftText = Convert.ToString(left, CultureInfo.CurrentCulture) ?? string.Empty;
                var rightText = Convert.ToString(right, CultureInfo.CurrentCulture) ?? string.Empty;
                compareResult = StringComparer.CurrentCultureIgnoreCase.Compare(
                    leftText,
                    rightText);
            }
            else if (left is IComparable comparable)
            {
                compareResult = comparable.CompareTo(right);
            }
            else
            {
                compareResult = StringComparer.CurrentCultureIgnoreCase.Compare(
                    Convert.ToString(left, CultureInfo.CurrentCulture),
                    Convert.ToString(right, CultureInfo.CurrentCulture));
            }

            return descending ? -compareResult : compareResult;
        }

        public static IReadOnlyList<string> ReviewStatusOptions { get; } =
            new[] { "未判定", "通过", "问题" };
    }
}
