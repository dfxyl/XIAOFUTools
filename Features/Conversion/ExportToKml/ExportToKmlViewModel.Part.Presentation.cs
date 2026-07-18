using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO.Compression;
using System.Xml;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Conversion.ExportToKml
{
    internal partial class ExportToKmlViewModel
    {

        public void RefreshLayers()
        {
            LoadFeatureLayers();
        }

        private void BrowseOutputFolder()
        {
            try
            {
                var selectedFolder = PresentationServices.Files.SelectFolder(
                    "选择输出文件夹",
                    _fileStore.DirectoryExists(OutputFolder) ? OutputFolder : null);
                if (!string.IsNullOrWhiteSpace(selectedFolder))
                {
                    OutputFolder = selectedFolder;
                    AddLog($"选择输出文件夹: {OutputFolder}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"选择输出文件夹出错: {ex.Message}");
            }
        }

        private void ShowHelp()
        {
            var helpMessage = "要素图层分组导出KML/KMZ工具使用说明：\n\n" +
                "1. 选择输入图层：从下拉列表中选择需要导出的要素图层\n" +
                "2. 选择分组字段：\n" +
                "   - 选择一个字段按其值进行分组导出\n" +
                "   - 选择[不分组]则将全部要素导出为单个文件\n" +
                "3. 选择导出格式：\n" +
                "   - KML：标准的 Keyhole 标记语言格式(XML格式，文件较大)\n" +
                "   - KMZ：压缩的 KML 格式(文件较小)\n" +
                "4. 选择输出文件夹：导出文件将保存到此文件夹\n" +
                "5. 点击导出按钮开始导出\n\n" +
                "注意：\n" +
                "- 导出调用 ArcGIS Pro 内置 Layer To KML 工具，自动处理坐标转换和符号样式\n" +
                "- 分组导出时，每个分组值将生成一个独立的文件\n" +
                "- 文件名基于图层名称和分组字段值生成\n" +
                "- 支持点、线、面要素导出\n" +
                "- 勾选生成文字标注层后，将按所选标注字段直接生成并合并标注点";

            PresentationServices.Dialogs.Show(helpMessage, "使用帮助");
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}\n";

            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent += logMessage;
            });
        }

        private async Task SelectSourceLayerByWhereAsync(FeatureLayer featureLayer, string whereClause, CancellationToken cancellationToken)
        {
            var env = Geoprocessing.MakeEnvironmentArray("addOutputsToMap", "False");
            var primaryParams = Geoprocessing.MakeValueArray(featureLayer, "NEW_SELECTION", whereClause);

            var primaryResult = await Geoprocessing.ExecuteToolAsync(
                SelectLayerByAttributeToolName,
                primaryParams,
                env,
                cancellationToken,
                null,
                GPExecuteToolFlags.None);

            if (primaryResult != null && !primaryResult.IsFailed)
            {
                return;
            }

            var fallbackParams = Geoprocessing.MakeValueArray(featureLayer, "NEW_SELECTION", whereClause);
            var fallbackResult = await Geoprocessing.ExecuteToolAsync(
                SelectLayerByAttributeLegacyToolName,
                fallbackParams,
                env,
                cancellationToken,
                null,
                GPExecuteToolFlags.None);

            if (fallbackResult == null || fallbackResult.IsFailed)
            {
                var message = BuildGpErrorMessage(fallbackResult ?? primaryResult);
                throw new InvalidOperationException($"按分组筛选源图层失败: {message}");
            }
        }

        private async Task ClearSourceLayerSelectionAsync(FeatureLayer featureLayer)
        {
            if (featureLayer == null)
            {
                return;
            }

            try
            {
                await QueuedTask.Run(() => featureLayer.ClearSelection());
            }
            catch
            {
                // 清理选择失败不影响主流程
            }
        }

        private static void UpdatePlacemarkNamesFromField(
            XmlDocument doc,
            XmlNamespaceManager nsManager,
            string kmlNs,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return;
            }

            var placemarks = doc.SelectNodes("//kml:Placemark", nsManager);
            if (placemarks == null || placemarks.Count == 0)
            {
                return;
            }

            foreach (XmlNode node in placemarks)
            {
                if (node is not XmlElement placemark)
                {
                    continue;
                }

                var value = ExtractFieldValueFromPlacemark(placemark, fieldName, kmlNs);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var nameElement = placemark["name", kmlNs];
                if (nameElement == null)
                {
                    nameElement = doc.CreateElement("name", kmlNs);
                    if (placemark.HasChildNodes)
                    {
                        placemark.InsertBefore(nameElement, placemark.FirstChild);
                    }
                    else
                    {
                        placemark.AppendChild(nameElement);
                    }
                }

                nameElement.InnerText = value;
            }
        }
    }
}
