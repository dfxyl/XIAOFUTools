#nullable enable

using System;
using System.Collections.Generic;
namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    public static class DownloadRequestValidator
    {
        public static HistoricalDownloadValidationResult Validate(HistoricalDownloadRequest request)
        {
            var errors = new List<string>();

            if (request.Version == null || string.IsNullOrWhiteSpace(request.Version.VersionId))
            {
                errors.Add("A historical version must be selected.");
            }

            if (request.ZoomLevel <= 0)
            {
                errors.Add("A valid zoom level is required.");
            }

            if (string.IsNullOrWhiteSpace(request.OutputFilePath))
            {
                errors.Add("An output file path is required.");
            }

            return new HistoricalDownloadValidationResult(errors.Count == 0, errors);
        }
    }

    public sealed class HistoricalDownloadValidationResult
    {
        public HistoricalDownloadValidationResult(bool isValid, IReadOnlyList<string> errors)
        {
            IsValid = isValid;
            Errors = errors;
        }

        public bool IsValid { get; }

        public IReadOnlyList<string> Errors { get; }
    }
}
