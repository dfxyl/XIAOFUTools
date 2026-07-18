using System;
using System.Collections.ObjectModel;

namespace XIAOFUTools.Features.Conversion.Cad.Shared
{
    /// <summary>
    /// CAD 图层按字段命名时使用的统一字段选项。
    /// </summary>
    public sealed class CadFieldOption
    {
        public string Name { get; set; }
        public string Alias { get; set; }
        public bool IsSelected { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? Name : Alias;

        public string FullDisplayName =>
            string.IsNullOrWhiteSpace(Alias) || string.Equals(Alias, Name, StringComparison.OrdinalIgnoreCase)
                ? Name
                : $"{Alias} [{Name}]";

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// DWG/DXF 导出器共同遵循的字段命名契约，避免功能间引用具体 ViewModel。
    /// </summary>
    internal interface ICadFieldNamingTarget
    {
        bool UseFieldNaming { get; set; }
        string FieldNamingSeparator { get; set; }
        ObservableCollection<CadFieldOption> NamingFields { get; }
    }
}
