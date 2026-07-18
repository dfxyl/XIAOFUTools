# 架构治理验收记录

## 自动化验收基线

验收环境：Windows x64、.NET 10、ArcGIS Pro 3.7.1，`ArcGISProInstallDir=D:\RUANJIAN\ArcGIS\Pro`。

以下项目由构建和测试自动验证：

- Debug/Release x64 编译和 `.esriAddinX` 打包。
- DAML 内部 `refID`、DockPane ID、类名解析和本地资源路径。
- `Shared` 不依赖 `Features`，功能域不显式依赖其他功能域的 Presentation/Infrastructure 类型。
- ViewModel 不直接调用桌面 UI API，不存在功能私有命令实现。
- 生产 C# 文件不超过 800 行，ViewModel 不超过 500 行，code-behind 不超过 300 行。
- Add-in 包包含主程序集、Config.daml、全部发布图标、数据文件和 AI Web 资源。
- 默认测试排除 Integration 和 LiveNetwork；本地文件依赖测试使用 Integration；真实服务测试使用 LiveNetwork，并配置 30 秒请求取消。

## ArcGIS Pro 宿主验收流程

宿主冒烟验证必须使用 Release `.esriAddinX` 和一份可回退的测试工程，按下列顺序执行并保存 ArcGIS Pro 日志：

1. 启动 ArcGIS Pro 3.7，确认加载 XIAOFU 工具箱选项卡且无 Add-in 加载错误。
2. 核对 DAML 注册：全部本地按钮/工具引用可见，59 个 DockPane 可按入口激活，图标和标题显示完整。
3. General：快速添加数据、在线影像或历史影像至少执行一项。
4. Analysis：查看面积或浏览要素至少执行一项。
5. Conversion：FeatureToTxt 和一个 Office/CAD 导出流程各执行一次。
6. DataManagement：图幅或批量数据处理至少执行一项。
7. Editing：面积分割、符号粘贴或界址工具至少执行一项，并验证撤销链。
8. Cartography：地图系列导出或布局坐标表至少执行一项。
9. Custom：开发区核查执行一次，验证取消、进度、结果和导出。
10. User：AI 助手新建会话、流式响应、设置保存、更新检查各执行一次。
11. 打开 GIS 工具口袋，验证添加工具箱、搜索调用、动态入口刷新、设置开关和旧 GIS Toolbox 配置读取。
12. 使用 1.2.8 的 settings.json 和 AI SQLite 数据库启动，确认原路径、字段、表结构和会话数据兼容。
13. 关闭并重新打开 ArcGIS Pro，确认设置持久化、无残留进程且 Add-in 可再次加载。

自动化验收不替代 ArcGIS Pro 宿主内的交互、Office COM 运行时和真实网络行为验证。
