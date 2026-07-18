using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt.Infrastructure
{
    internal sealed class FeatureTextFileWriter
    {
        public async Task WriteAsync(
            string content,
            string filePath,
            Encoding encoding,
            string formatName,
            Action<string> logInfo,
            Action<string> logError)
        {
            try
            {
                logInfo($"准备保存文件: {filePath}");
                logInfo($"文件内容长度: {content.Length} 字符");
                logInfo($"文件内容预览: {content[..Math.Min(200, content.Length)]}...");
                logInfo($"使用文本格式: {formatName}, 编码: {encoding.EncodingName}");
                logInfo($"编码详细信息: CodePage={encoding.CodePage}, BodyName={encoding.BodyName}");
                ValidateEncoding(content, encoding, formatName, logInfo, logError);

                await File.WriteAllTextAsync(filePath, content, encoding);
                var savedBytes = await File.ReadAllBytesAsync(filePath);
                logInfo($"文件保存后前10字节: {string.Join(" ", savedBytes.Take(10).Select(value => value.ToString("X2")))}");
                LogDetectedEncoding(savedBytes, formatName, logInfo);

                if (!File.Exists(filePath))
                {
                    logError($"文件保存失败: 文件不存在 {filePath}");
                    return;
                }

                logInfo($"文件保存成功: {filePath}, 大小: {new FileInfo(filePath).Length} 字节");
            }
            catch (Exception ex)
            {
                logError($"保存文件失败: {ex.Message}");
                logError($"异常详情: {ex}");
            }
        }

        public void Write(
            string content,
            string filePath,
            Encoding encoding,
            Action<string> logError)
        {
            try
            {
                File.WriteAllText(filePath, content, encoding);
            }
            catch (Exception ex)
            {
                logError($"保存文件失败: {ex.Message}");
            }
        }

        private static void ValidateEncoding(
            string content,
            Encoding encoding,
            string formatName,
            Action<string> logInfo,
            Action<string> logError)
        {
            try
            {
                var encodedBytes = encoding.GetBytes(content);
                var decodedContent = encoding.GetString(encodedBytes);
                if (decodedContent == content)
                {
                    logInfo($"编码验证通过: 内容可以正确编码为 {formatName}");
                }
                else
                {
                    logError($"警告: 内容在 {formatName} 编码下可能存在字符丢失");
                }
            }
            catch (Exception ex)
            {
                logError($"编码验证失败: {ex.Message}");
            }
        }

        private static void LogDetectedEncoding(byte[] bytes, string formatName, Action<string> logInfo)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                logInfo("检测到UTF-8 BOM - 文件编码正确");
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                logInfo("检测到UTF-16LE BOM - 文件编码正确");
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                logInfo("检测到UTF-16BE BOM - 文件编码正确");
            }
            else if (string.Equals(formatName, "UTF-8(无BOM)", StringComparison.OrdinalIgnoreCase))
            {
                logInfo("UTF-8无BOM编码 - 文件编码正确");
            }
            else if (string.Equals(formatName, "ANSI", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(formatName, "GBK", StringComparison.OrdinalIgnoreCase))
            {
                logInfo($"{formatName}编码 - 文件编码正确");
            }
            else
            {
                logInfo("未检测到BOM，请确认编码格式是否正确");
            }
        }
    }
}
