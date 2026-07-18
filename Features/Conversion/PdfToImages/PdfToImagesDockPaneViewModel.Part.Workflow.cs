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

namespace XIAOFUTools.Features.Conversion.PdfToImages
{
    internal partial class PdfToImagesDockPaneViewModel
    {

        /// <summary>
        /// 处理PDF文件（简化并行版本）
        /// </summary>
        private void ProcessPdfFiles(CancellationToken token)
        {
            var request = new Infrastructure.PdfImageConversionRequest(
                InputFolder,
                OutputFolder,
                SaveToSourcePath,
                KeepOriginalStructure,
                CreateSeparateFolder,
                TraverseSubfolders,
                Resolution,
                OutputFormat);
            _conversionService.Convert(request, token, progress =>
            {
                if (!string.IsNullOrWhiteSpace(progress.Message))
                {
                    if (progress.IsError)
                    {
                        LogError(progress.Message);
                    }
                    else
                    {
                        LogMessage(progress.Message);
                    }
                }

                if (progress.TotalFiles > 0 && progress.ProcessedFiles > 0)
                {
                    PresentationServices.UiThread.Post(() =>
                    {
                        Progress = (double)progress.ProcessedFiles / progress.TotalFiles * 100;
                        StatusText = $"已处理 {progress.ProcessedFiles}/{progress.TotalFiles} 个文件";
                    });
                }
            });
        }
    }
}
