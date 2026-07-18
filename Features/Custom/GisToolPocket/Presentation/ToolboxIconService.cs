using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using XIAOFUTools.Features.Custom.GisToolPocket.Application;
using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Presentation
{
    internal static class ToolboxIconService
    {
        public const string ToolsetIconBaseName = "ToolsetKind";
        public const string ToolIconBaseName = "ToolKind";

        private static readonly string AssemblyName = typeof(ToolboxIconService).Assembly.GetName().Name?.Replace(" ", "%20") ?? "XIAOFUTools";

        public static string SlotIconBaseName(int index)
        {
            return $"Slot{Math.Clamp(index, 1, ToolboxMenuSlotService.MaxSlots):00}";
        }

        public static string NumberedIconBaseName(int index)
        {
            var normalized = ((Math.Max(1, index) - 1) % ToolboxMenuSlotService.MaxSlots) + 1;
            return SlotIconBaseName(normalized);
        }

        public static string ResolveIconBaseName(string text)
        {
            return ToolIconBaseName;
        }

        public static string SmallImage(string iconBaseName)
        {
            return $"Assets\\Images\\GisToolPocket\\{Normalize(iconBaseName)}16.png";
        }

        public static string SmallImageUri(string iconBaseName)
        {
            return BuildPackUri(Normalize(iconBaseName), 16);
        }

        public static string LargeImage(string iconBaseName)
        {
            return $"Assets\\Images\\GisToolPocket\\{Normalize(iconBaseName)}32.png";
        }

        public static ImageSource SmallImageSource(string iconBaseName)
        {
            return LoadImageSource(Normalize(iconBaseName), 16);
        }

        public static ImageSource LargeImageSource(string iconBaseName)
        {
            return LoadImageSource(Normalize(iconBaseName), 32);
        }

        private static string Normalize(string iconBaseName)
        {
            return string.IsNullOrWhiteSpace(iconBaseName) ? "Slot01" : iconBaseName;
        }

        private static ImageSource LoadImageSource(string iconBaseName, int size)
        {
            var uri = new Uri(BuildPackUri(iconBaseName, size), UriKind.Absolute);
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static string BuildPackUri(string iconBaseName, int size)
        {
            return $"pack://application:,,,/{AssemblyName};component/Assets/Images/GisToolPocket/{iconBaseName}{size}.png";
        }
    }
}
