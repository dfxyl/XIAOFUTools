using System.IO;

namespace XIAOFUTools.Features.Conversion.PolygonToDwgWithFill
{
    internal partial class PolygonToDwgWithFillDockPaneViewModel
    {
        private static string SanitizeFileName(string name)
        {
            foreach (var character in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(character, '_');
            }

            return name;
        }
    }
}
