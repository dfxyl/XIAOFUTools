using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace XIAOFUTools.Features.Conversion.ImagesToPdf.Infrastructure
{
    internal sealed class ImagePdfDocumentWriter
    {
        private readonly ImagePdfFileStore _fileStore;

        internal ImagePdfDocumentWriter(ImagePdfFileStore fileStore)
        {
            _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        }

        internal void Write(
            IReadOnlyList<string> imageFiles,
            string outputPath,
            int dpi,
            CancellationToken cancellationToken,
            Action<string> log,
            Action<string> logError)
        {
            ArgumentNullException.ThrowIfNull(imageFiles);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpi);
            log ??= _ => { };
            logError ??= _ => { };
            if (imageFiles.Count == 0)
            {
                return;
            }

            _fileStore.EnsureParentDirectory(outputPath);
            using var document = new PdfDocument();
            document.Info.Title = Path.GetFileNameWithoutExtension(outputPath);
            document.Info.Creator = "XIAOFUTools";

            for (var index = 0; index < imageFiles.Count; index++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var imageFile = imageFiles[index];
                try
                {
                    using var image = Image.FromFile(imageFile);
                    var pageWidth = image.Width / (double)dpi * 72.0;
                    var pageHeight = image.Height / (double)dpi * 72.0;
                    var page = document.AddPage();
                    page.Width = XUnit.FromPoint(pageWidth);
                    page.Height = XUnit.FromPoint(pageHeight);
                    using var graphics = XGraphics.FromPdfPage(page);
                    using var pdfImage = XImage.FromFile(imageFile);
                    graphics.DrawImage(pdfImage, 0, 0, pageWidth, pageHeight);
                    log(
                        $"  第{index + 1}页: {Path.GetFileName(imageFile)} - 像素: " +
                        $"{image.Width:F0}x{image.Height:F0}, 页面: {pageWidth:F2}x{pageHeight:F2}点 " +
                        $"({pageWidth / 72.0:F2}x{pageHeight / 72.0:F2}英寸)");
                }
                catch (Exception exception)
                {
                    logError($"添加图片 {Path.GetFileName(imageFile)} 到PDF时出错: {exception.Message}");
                }
            }

            if (document.PageCount > 0)
            {
                document.Save(outputPath);
            }
        }
    }
}
