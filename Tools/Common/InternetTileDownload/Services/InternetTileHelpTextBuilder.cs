#nullable enable

namespace XIAOFUTools.Tools.InternetTileDownload.Services
{
    internal static class InternetTileHelpTextBuilder
    {
        public static string Build()
        {
            return "互联网切片下载使用说明：\n\n"
                + "1. 支持的服务类型：WMTS、XYZ。\n"
                + "2. 粘贴完整的切片模板链接，工具会自动识别服务类型和层级。\n"
                + "3. 下载范围支持当前视图、框选范围、面图层范围。\n"
                + "4. 输出结果为 GeoTIFF，可按所选坐标系自动转换。\n"
                + "5. 面图层范围会执行精确裁切，矩形范围按视图或框选范围下载。";
        }
    }
}
