using System;
using System.Text;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    internal partial class FeatureToTxtDockPaneViewModel
    {
        private Encoding GetEncodingFromFormat(string format)
        {
            try
            {
                return format?.ToUpperInvariant() switch
                {
                    "ANSI" or "GBK" => Encoding.GetEncoding("GBK"),
                    "UNICODE" => Encoding.Unicode,
                    "UTF-8(无BOM)" => new UTF8Encoding(false),
                    _ => new UTF8Encoding(true)
                };
            }
            catch (Exception ex)
            {
                LogError($"获取编码失败，使用UTF-8: {ex.Message}");
                return new UTF8Encoding(true);
            }
        }

        private string GenerateFileHeader()
        {
            if (SelectedHeaderConfig != null && !string.IsNullOrWhiteSpace(SelectedHeaderConfig.HeaderText))
            {
                return SelectedHeaderConfig.HeaderText;
            }

            return @"[属性描述]
坐标系=2000国家大地坐标系
几度分带=3
投影类型=高斯克吕格
计量单位=米
带号=37
精度=0.001
转换参数=,,,,,,
[地块坐标]";
        }
    }
}
