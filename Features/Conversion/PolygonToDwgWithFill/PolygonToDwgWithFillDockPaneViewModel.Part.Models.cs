using System;
using XIAOFUTools.Features.Conversion.PolygonToDwgWithFill.Core;

namespace XIAOFUTools.Features.Conversion.PolygonToDwgWithFill
{
    internal partial class PolygonToDwgWithFillDockPaneViewModel
    {
        
        /// <summary>
        /// DWG 版本选项（显示名 + 枚举值）
        /// </summary>
        public class DwgVersionOption
        {
            public string Name { get; set; }
            public DwgExportVersion Version { get; set; }
            public override string ToString() => Name;
        }
    }
}
