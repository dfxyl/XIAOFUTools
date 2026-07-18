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
        /// 合并模式处理图片
        /// </summary>
        private void ProcessImagesInMergeMode(CancellationToken token)
        {
            // 获取所有文件夹
            List<string> folders = new List<string>();
            
            if (TraverseSubfolders)
            {
                // 获取所有子文件夹
                folders.AddRange(_fileStore.GetFolders(InputFolder, traverseSubfolders: true).Skip(1));
            }
            
            // 总是包含根文件夹
            folders.Insert(0, InputFolder);

            LogMessage($"找到 {folders.Count} 个文件夹，开始处理...");

            int totalFolders = folders.Count;
            int processedFolders = 0;

            foreach (var folder in folders)
            {
                if (token.IsCancellationRequested)
                    break;

                // 获取当前文件夹中的所有图片（不包括子文件夹）
                var imageFiles = GetImageFilesInFolder(folder, includeSubfolders: false);

                if (imageFiles.Count == 0)
                {
                    processedFolders++;
                    Progress = (double)processedFolders / totalFolders * 100;
                    continue;
                }

                try
                {
                    string folderName = folder == InputFolder ? Path.GetFileName(InputFolder) : Path.GetFileName(folder);
                    LogMessage($"正在处理文件夹: {folderName} ({imageFiles.Count} 张图片)");

                    // 确定输出路径，使用文件夹名称作为PDF文件名
                    string pdfFileName = folderName ?? "output";
                    string outputPath = GetOutputPdfPath(folder, pdfFileName);
                    
                    // 创建PDF并添加所有图片
                    CreatePdfFromImages(imageFiles, outputPath, token);
                    
                    LogMessage($"完成处理: {folderName} -> {Path.GetFileName(outputPath)}");
                }
                catch (Exception ex)
                {
                    LogError($"处理文件夹 {Path.GetFileName(folder)} 时出错: {ex.Message}");
                }

                processedFolders++;
                Progress = (double)processedFolders / totalFolders * 100;
                StatusText = $"已处理 {processedFolders}/{totalFolders} 个文件夹";
            }
        }

        /// <summary>
        /// 从图片创建PDF
        /// </summary>
        private void CreatePdfFromImages(IReadOnlyList<string> imageFiles, string outputPath, CancellationToken token) =>
            _documentWriter.Write(imageFiles, outputPath, Dpi, token, LogMessage, LogError);
    }
}
