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
        /// 处理图片文件
        /// </summary>
        private void ProcessImages(CancellationToken token)
        {
            if (MergeImages)
            {
                // 合并模式：按文件夹分组处理
                ProcessImagesInMergeMode(token);
            }
            else
            {
                // 非合并模式：每张图片单独生成PDF
                ProcessImagesInSeparateMode(token);
            }
        }

        /// <summary>
        /// 非合并模式处理图片
        /// </summary>
        private void ProcessImagesInSeparateMode(CancellationToken token)
        {
            // 获取所有图片文件
            var imageFiles = GetImageFilesInFolder(InputFolder, TraverseSubfolders);

            if (imageFiles.Count == 0)
            {
                LogMessage("未找到图片文件");
                return;
            }

            LogMessage($"找到 {imageFiles.Count} 个图片文件，开始处理...");

            int totalFiles = imageFiles.Count;
            int processedFiles = 0;

            foreach (var imageFile in imageFiles)
            {
                if (token.IsCancellationRequested)
                    break;

                try
                {
                    string imageName = Path.GetFileName(imageFile);
                    string pdfName = Path.GetFileNameWithoutExtension(imageFile);
                    LogMessage($"正在处理: {imageName} -> {pdfName}.pdf");

                    // 确定输出路径（使用图片名称作为PDF文件名）
                    string outputPath = GetOutputPdfPath(Path.GetDirectoryName(imageFile), pdfName);
                    
                    // 创建PDF（单张图片）
                    CreatePdfFromImages(new List<string> { imageFile }, outputPath, token);
                    
                    LogMessage($"  - 已生成: {Path.GetFileName(outputPath)}");
                }
                catch (Exception ex)
                {
                    LogError($"处理文件 {Path.GetFileName(imageFile)} 时出错: {ex.Message}");
                }

                processedFiles++;
                Progress = (double)processedFiles / totalFiles * 100;
                StatusText = $"已处理 {processedFiles}/{totalFiles} 个文件";
            }
        }
    }
}
