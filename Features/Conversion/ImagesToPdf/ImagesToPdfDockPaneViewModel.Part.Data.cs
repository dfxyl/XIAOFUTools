using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.ImagesToPdf
{
    internal partial class ImagesToPdfDockPaneViewModel
    {

        /// <summary>
        /// 获取文件夹中的所有图片文件
        /// </summary>
        private IReadOnlyList<string> GetImageFilesInFolder(string folder, bool includeSubfolders)
        {
            return _fileStore.GetImageFiles(folder, includeSubfolders, SupportedImageExtensions);
        }

        /// <summary>
        /// 获取输出PDF路径
        /// </summary>
        private string GetOutputPdfPath(string sourceFolderPath, string pdfName)
        {
            return _fileStore.CreateUniquePdfOutputPath(new Infrastructure.ImagePdfOutputPathRequest(
                sourceFolderPath,
                InputFolder,
                OutputFolder,
                SaveToSourcePath,
                KeepOriginalStructure,
                pdfName));
        }
    }
}
