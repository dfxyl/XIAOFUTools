#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

using XIAOFUTools.Features.Custom.GisToolPocket.Application;
using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Presentation
{
    public partial class ToolboxDonationWindow : Window
    {
        private const string EmbeddedDonationResourceName = "XIAOFUTools.Features.Custom.GisToolPocket.Images.donation.jpg";

        public ToolboxDonationWindow()
        {
            InitializeComponent();
            LoadDonationImage();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadDonationImage()
        {
            try
            {
                using var embeddedStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedDonationResourceName);
                if (embeddedStream is not null)
                {
                    DonationImage.Source = CreateBitmap(embeddedStream);
                    return;
                }

                var streamInfo = System.Windows.Application.GetResourceStream(new Uri("Assets/Images/GisToolPocket/donation.jpg", UriKind.Relative));
                if (streamInfo?.Stream is not null)
                {
                    using var stream = streamInfo.Stream;
                    DonationImage.Source = CreateBitmap(stream);
                    return;
                }

                streamInfo = System.Windows.Application.GetResourceStream(new Uri("Assets/Images/GisToolPocket/赞赏.jpg", UriKind.Relative));
                if (streamInfo?.Stream is not null)
                {
                    using var stream = streamInfo.Stream;
                    DonationImage.Source = CreateBitmap(stream);
                    return;
                }

                var assemblyName = Uri.EscapeDataString(typeof(ToolboxDonationWindow).Assembly.GetName().Name ?? "XIAOFUTools");
                var uri = new Uri($"pack://application:,,,/{assemblyName};component/Assets/Images/GisToolPocket/donation.jpg", UriKind.Absolute);
                DonationImage.Source = new BitmapImage(uri);
            }
            catch
            {
                DonationImage.Source = null;
                DonationImage.ToolTip = "赞赏码图片加载失败，请确认嵌入资源 Assets\\Images\\GisToolPocket\\donation.jpg 已包含在加载项包中。";
            }
        }

        private static BitmapImage CreateBitmap(Stream stream)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
