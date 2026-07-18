using System.Collections.Generic;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt.Core
{
    internal static class FeatureToTxtFieldCatalog
    {
        internal static IReadOnlyDictionary<string, IReadOnlyList<string>> Mappings { get; } =
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["地块面积"] = new[] { "地块面积", "面积", "MIANJI", "MJ" },
                ["地块编号"] = new[] { "地块编号", "编号", "地块号", "BIANHAO", "BH", "ID" },
                ["地块名称"] = new[] { "地块名称", "名称", "地名", "MINGCHENG", "MC", "NAME" },
                ["图幅号"] = new[] { "图幅号", "图幅", "TUFUHAO", "TFH", "MAPSHEET" },
                ["地块用途"] = new[] { "地块用途", "用途", "土地用途", "YONGTU", "YT", "LANDUSE" },
                ["地类编码"] = new[] { "地类编码", "地类", "编码", "DILEI", "DL", "LANDCODE", "CODE" }
            };
    }
}
