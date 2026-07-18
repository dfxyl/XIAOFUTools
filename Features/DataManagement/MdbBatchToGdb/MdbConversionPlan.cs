namespace XIAOFUTools.Features.DataManagement.MdbBatchToGdb
{
    internal sealed class MdbConversionPlan
    {
        public MdbConversionPlan(MdbFileItem item, string outputPath, MdbOutputFormat outputFormat)
        {
            Item = item;
            OutputPath = outputPath;
            OutputFormat = outputFormat;
        }

        public MdbFileItem Item { get; }

        public string OutputPath { get; }

        public MdbOutputFormat OutputFormat { get; }
    }
}
