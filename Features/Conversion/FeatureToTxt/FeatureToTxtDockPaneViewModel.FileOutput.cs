using System.Threading.Tasks;
using XIAOFUTools.Features.Conversion.FeatureToTxt.Infrastructure;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    internal partial class FeatureToTxtDockPaneViewModel
    {
        private readonly FeatureTextFileWriter _fileWriter = new();

        private Task SaveToFile(string content, string filePath)
        {
            return _fileWriter.WriteAsync(
                content,
                filePath,
                GetEncodingFromFormat(SelectedTextFormat),
                SelectedTextFormat,
                message => LogInfo(message),
                LogError);
        }

        private void SaveToFileSync(string content, string filePath)
        {
            _fileWriter.Write(
                content,
                filePath,
                GetEncodingFromFormat(SelectedTextFormat),
                LogError);
        }
    }
}
