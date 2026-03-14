#nullable enable

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    internal static class HistoricalImageryHelpTextBuilder
    {
        public static string Build()
        {
            return "1. 选择数据源与下载级别。\n"
                + "2. 设置查询模式：‘查询历史’表示仅显示变化版本，‘查询全部’表示显示全部可用版本。\n"
                + "3. 点击‘查询列表’，系统会基于当前地图中心点查询历史版本。\n"
                + "4. Wayback 在‘查询历史’模式下会请求元数据筛选变化版本；Google 当前两个模式返回结果相同。\n"
                + "5. 选择范围来源，可使用当前视图、地图框选或面图层范围。\n"
                + "6. 勾选一个或多个历史版本，选择输出文件夹与输出坐标系后开始下载。\n"
                + "7. 批量下载会按所选日期分别输出 GeoTIFF 文件。\n\n"
                + "注意事项：\n"
                + "- Google 服务需要特殊网络环境，否则可能无法查询或下载。\n"
                + "- Wayback 在‘查询历史’模式下需要请求较多元数据，首次查询可能比‘查询全部’更慢。\n"
                + "- 若输出文件已加载到地图中，工具会自动移除旧图层后再写入。";
        }
    }
}
