namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal sealed class MdbConversionPlan
    {
        public MdbConversionPlan(MdbFileItem item, string outputPath)
        {
            Item = item;
            OutputPath = outputPath;
        }

        public MdbFileItem Item { get; }

        public string OutputPath { get; }
    }
}
