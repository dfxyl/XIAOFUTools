using System;
using System.Collections.Generic;
using System.IO;

namespace XIAOFUTools.Features.Conversion.PolygonToDxfWithFill
{
    internal partial class PolygonToDxfWithFillDockPaneViewModel
    {
        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static string SanitizeLayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Layer0";
            var invalid = new char[] { '<','>','/','\\',':','\"','?','*','|',',',';','=','[',']','{','}','(',')' };
            foreach (var c in invalid)
                name = name.Replace(c, '_');
            name = name.Replace(' ', '_');
            if (name.Length > 60)
                name = name.Substring(0, 60);
            return name;
        }

    }
}
