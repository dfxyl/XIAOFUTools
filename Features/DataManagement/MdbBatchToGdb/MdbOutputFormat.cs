namespace XIAOFUTools.Features.DataManagement.MdbBatchToGdb
{
    internal enum MdbOutputFormat
    {
        FileGeodatabase,
        MobileGeodatabase,
        XmlWorkspaceDocument
    }

    internal sealed class MdbOutputFormatOption
    {
        public MdbOutputFormatOption(MdbOutputFormat format, string displayName)
        {
            Format = format;
            DisplayName = displayName;
        }

        public MdbOutputFormat Format { get; }

        public string DisplayName { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    internal static class MdbOutputFormatExtensions
    {
        public static string GetExtension(this MdbOutputFormat format)
        {
            return format switch
            {
                MdbOutputFormat.FileGeodatabase => ".gdb",
                MdbOutputFormat.MobileGeodatabase => ".geodatabase",
                MdbOutputFormat.XmlWorkspaceDocument => ".xml",
                _ => throw new System.ArgumentOutOfRangeException(nameof(format), format, null)
            };
        }

        public static string GetGeoprocessingOutputFormat(this MdbOutputFormat format)
        {
            return format switch
            {
                MdbOutputFormat.FileGeodatabase => "FILE_GDB",
                MdbOutputFormat.MobileGeodatabase => "MOBILE_GDB",
                MdbOutputFormat.XmlWorkspaceDocument => "XML",
                _ => throw new System.ArgumentOutOfRangeException(nameof(format), format, null)
            };
        }
    }
}
