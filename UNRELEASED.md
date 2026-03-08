# 未发布的更改

记录开发中的修改，发布新版本时将内容移入 [CHANGELOG.md](CHANGELOG.md)。

**最后更新**: 2026-03-08

---

## 新增功能

### Ribbon 分组与文档同步整理 (2026-03-08)
- 将加载项主 Ribbon 压缩为 `通用 / 编辑 / 数据 / 制图 / 系统` 5 组。
- `查看面积`、`添加预设图层`、`历史影像` 保持在通用组直达入口。
- `驱动制图` 调整并入 `制图 > 布局图幅` 面板，不再单独占用顶级按钮。
- 同步更新 `README.md`、`Docs/USER_GUIDE.md`、`AGENTS.md` 中的工具箱结构与目录说明。

### MDB批量转GDB工具（数据 > 数据整备）(2026-03-05)
- 新增“MDB批量转GDB”工具，支持选择文件夹后自动遍历并列出 `.mdb` 文件，可按需勾选批量转换。
- 支持“遍历子文件夹”、全选/反选/清空、输出到源路径或指定目录、覆盖确认、停止取消、进度与日志。
- 转换引擎基于 GDAL（PGeo/FileGDB/OpenFileGDB），支持要素数据集、根目录要素类与表的批量迁移。
- 工具代码按模块拆分，避免单文件过长，便于后续维护与扩展。
- 修改的文件:
  - `Config.daml` - 新增按钮、停靠窗格注册，并挂接到“数据整备”按钮面板
  - `XIAOFUTools.csproj` - 新增 GDAL/GDAL.Native 依赖
  - `Tools/DataProcessing/MdbBatchToGdb/MdbBatchToGdbButton.cs` - 新增按钮入口与授权校验
  - `Tools/DataProcessing/MdbBatchToGdb/MdbBatchToGdbDockPane.cs` - 新增停靠窗格容器
  - `Tools/DataProcessing/MdbBatchToGdb/MdbBatchToGdbDockPaneView.xaml` - 新增工具界面
  - `Tools/DataProcessing/MdbBatchToGdb/MdbBatchToGdbDockPaneView.xaml.cs` - 新增视图初始化与布尔反转转换器
  - `Tools/DataProcessing/MdbBatchToGdb/MdbBatchToGdbViewModel.cs` - 新增扫描、选择、转换调度、取消与日志流程
  - `Tools/DataProcessing/MdbBatchToGdb/GdalRuntimeBootstrapper.cs` - 新增 GDAL 运行时初始化与环境配置
  - `Tools/DataProcessing/MdbBatchToGdb/GdalMdbLayerInspector.cs` - 新增 MDB 图层/要素集元数据解析
  - `Tools/DataProcessing/MdbBatchToGdb/GdalMdbToGdbConverter.cs` - 新增批量转换核心流程
  - `Tools/DataProcessing/MdbBatchToGdb/OgrUtf8Interop.cs` - 新增 OGR 编码互操作封装
  - `Tools/DataProcessing/MdbBatchToGdb/RelayCommand.cs` - 新增命令实现
  - `Tools/DataProcessing/MdbBatchToGdb/MdbFileItem.cs` - 新增 MDB 列表项模型
  - `Tools/DataProcessing/MdbBatchToGdb/MdbFileDiscovery.cs` - 新增 MDB 文件遍历逻辑
  - `Tools/DataProcessing/MdbBatchToGdb/MdbConversionPlan.cs` - 新增转换计划模型
  - `Tools/DataProcessing/MdbBatchToGdb/MdbConversionPlanner.cs` - 新增输出规划与重名处理逻辑

### 批量合并SHP工具（数据 > 数据整备）(2026-03-04)
- 新增“批量合并SHP”工具，支持选择文件夹后自动遍历并列出 `.shp` 文件。
- 支持“是否读取子文件夹”开关，切换后自动重新扫描；扫描结果默认全选。
- 新增快捷勾选“点/线/面”按钮，可一键仅勾选对应几何类型。
- 合并链路使用 `Merge_management`，自动进行字段并集合并。
- 新增“是否创建源文件名字段”选项：开启后基于 `MERGE_SRC` 自动写入源文件名字段。
- 新增几何类型严格校验：若选中项存在未知类型或混合类型，运行按钮不可用并阻止合并。
- 支持输出位置选择（GDB 要素类或 Shapefile），并支持输出覆盖确认。
- 修改的文件:
  - `Config.daml` - 新增工具按钮、停靠窗格注册，并挂接到“数据整备”按钮面板
  - `Tools/DataProcessing/BatchMergeShp/BatchMergeShpButton.cs` - 新增按钮入口与授权校验
  - `Tools/DataProcessing/BatchMergeShp/BatchMergeShpDockPane.cs` - 新增停靠窗格容器
  - `Tools/DataProcessing/BatchMergeShp/BatchMergeShpDockPaneView.xaml` - 新增工具界面与快捷勾选按钮
  - `Tools/DataProcessing/BatchMergeShp/BatchMergeShpDockPaneView.xaml.cs` - 新增视图初始化逻辑与布尔反转转换器
  - `Tools/DataProcessing/BatchMergeShp/BatchMergeShpViewModel.cs` - 新增扫描、选择、几何校验、合并、字段写入与日志流程

### 文档批量替换工具（制图 > 文档处理）(2026-02-10)
- 新增“文档批量替换”工具，支持 `.docx/.doc/.docm` 多文件批量查找替换；支持添加文件、添加文件夹与拖拽到文件列表导入。
- 支持多条替换规则与匹配选项（区分大小写、全字匹配、区分全/半角、通配符），并提供“另存为副本/覆盖原文件”两种保存方式。
- 文档分组面板命名由“PDF相关”调整为“文档相关”，并在该面板新增工具入口。
- 处理链路稳定性增强：替换任务改为 STA 线程执行，按文件创建与释放 Word COM 实例，修复批处理中 `0x800706BE` 远程过程调用失败导致的中断问题。
- 界面布局优化：取消独立拖拽接收区，文件多时在列表内部滚动；日志框高度下调，避免挤占参数区。
- 新增专用图标资源（SVG + 16/32 PNG），并将工具按钮图标切换为专用图标。
- 修改的文件:
  - `Config.daml` - 新增按钮/停靠窗格注册，面板文案改为“文档相关”，并切换专用图标
  - `Tools/Convert/DocumentBatchReplace/DocumentBatchReplaceButton.cs` - 新增按钮入口与授权校验
  - `Tools/Convert/DocumentBatchReplace/DocumentBatchReplaceDockPane.cs` - 新增停靠窗格容器
  - `Tools/Convert/DocumentBatchReplace/DocumentBatchReplaceDockPaneView.xaml` - 新增并优化界面布局（文件区/规则区/日志区）
  - `Tools/Convert/DocumentBatchReplace/DocumentBatchReplaceDockPaneView.xaml.cs` - 新增视图初始化与拖拽接收处理
  - `Tools/Convert/DocumentBatchReplace/DocumentBatchReplaceDockPaneViewModel.cs` - 新增批量替换主流程、STA 执行、COM 稳定性处理与日志进度
  - `IconTools/Icons/DocumentBatchReplace.svg` - 新增矢量图标源文件
  - `Images/DocumentBatchReplace_16.png` - 新增小图标
  - `Images/DocumentBatchReplace_32.png` - 新增大图标
  - `XIAOFUTools.csproj` - 添加新图标内容清单
  - `AGENTS.md` - 工具箱结构文案由“PDF相关”更新为“文档相关”

### 属性表建SHP工具（数据 > 数据整备）(2026-02-10)
- 新增“属性表建SHP”工具，支持读取 Excel 属性结构模板并批量创建 Shapefile。
- 复用“属性表建库”模板体系，支持中文几何/字段类型解析，并提供坐标系选择、日志输出、停止执行与模板导出能力。
- 针对 SHP 约束新增自动兼容：忽略要素集定义、字段名自动压缩到 10 字符并去重、跳过 BLOB/GUID 等不支持字段类型。
- 新增专用模板 `建SHP模板.xls`，并将“导出模板”改为导出该模板，便于直接用于属性表建SHP。
- 修改的文件:
  - `Config.daml` - 新增按钮、停靠窗格与“数据整备”面板入口
  - `Tools/DataProcessing/ShapefileBuilder/ShapefileBuilderButton.cs` - 新增按钮入口与授权校验
  - `Tools/DataProcessing/ShapefileBuilder/ShapefileBuilderDockPane.cs` - 新增停靠窗格容器
  - `Tools/DataProcessing/ShapefileBuilder/ShapefileBuilderDockPaneView.xaml` - 新增工具界面与参数项
  - `Tools/DataProcessing/ShapefileBuilder/ShapefileBuilderDockPaneView.xaml.cs` - 新增视图初始化逻辑与布尔反转转换器
  - `Tools/DataProcessing/ShapefileBuilder/ShapefileBuilderViewModel.cs` - 新增建SHP流程、模板解析、字段类型转换与日志管理
  - `Data/Excel模板/建SHP模板.xls` - 新增属性表建SHP专用模板
  - `XIAOFUTools.csproj` - 新增建SHP模板复制到输出目录配置
  - `README.md` - 更新功能清单
  - `Docs/USER_GUIDE.md` - 新增“属性表建SHP”使用说明

### SHP目录处理工具（数据 > 数据整备）(2026-02-10)
- 新增“SHP输字段表”工具，支持按文件夹批量读取 Shapefile 字段结构并导出为 Excel 字段表。
- 修改的文件:
  - `Config.daml` - 新增“SHP输字段表”按钮、停靠窗格与“数据整备”面板入口
  - `Tools/DataProcessing/ExportShpFieldTable/ExportShpFieldTableButton.cs` - 新增按钮入口与授权校验
  - `Tools/DataProcessing/ExportShpFieldTable/ExportShpFieldTableDockPane.cs` - 新增停靠窗格容器
  - `Tools/DataProcessing/ExportShpFieldTable/ExportShpFieldTableDockPaneView.xaml` - 新增工具界面
  - `Tools/DataProcessing/ExportShpFieldTable/ExportShpFieldTableDockPaneView.xaml.cs` - 新增视图初始化逻辑
  - `Tools/DataProcessing/ExportShpFieldTable/ExportShpFieldTableViewModel.cs` - 新增SHP结构读取、Excel导出与日志控制
  - `README.md` - 更新功能清单
  - `Docs/USER_GUIDE.md` - 新增“SHP输字段表”使用说明

### 提取面扣岛工具（数据 > 分析检查）(2026-02-10)
- 新增“提取面扣岛”工具，支持从输入面要素中提取所有扣洞并输出为面要素图层。
- 输出结果保留源图层属性；多个洞可按开关选择逐洞输出或按源要素合并为多部件输出。
- 输出路径增强：非 GDB 路径自动规范为 `.shp`，该规范化仅用于处理，不再单独写入日志。
- 修改的文件:
  - `Config.daml` - 新增按钮、停靠窗格与“分析检查”面板入口
  - `Tools/Analysis/ExtractPolygonHoles/ExtractPolygonHolesButton.cs` - 新增按钮入口与授权校验
  - `Tools/Analysis/ExtractPolygonHoles/ExtractPolygonHolesDockPane.cs` - 新增停靠窗格容器
  - `Tools/Analysis/ExtractPolygonHoles/ExtractPolygonHolesDockPaneView.xaml` - 新增工具界面与参数项
  - `Tools/Analysis/ExtractPolygonHoles/ExtractPolygonHolesDockPaneView.xaml.cs` - 新增视图初始化逻辑
  - `Tools/Analysis/ExtractPolygonHoles/ExtractPolygonHolesViewModel.cs` - 新增扣洞提取、属性复制、输出路径规范化与运行流程
  - `Tools/Analysis/ExtractPolygonHoles/RelayCommand.cs` - 新增命令实现

### 数据透视工具（数据 > 分析检查）(2026-02-09)
- 新增“数据透视”工具，支持从地图中的要素图层/独立表选择输入数据。
- 支持多区域字段分组、透视字段展开、数值字段汇总，汇总方式支持：求和、计数、平均值、最大值、最小值、中位数、极差、标准差、方差。
- 新增默认命名规则：输出表名自动为 `TS_输入数据名称`，并在窗格加载时自动回到项目默认 GDB。
- 当前 Ribbon 中已归入“数据 > 分析检查”面板。
- 修改的文件:
  - `Tools/Analysis/DataPivot/DataPivotButton.cs` - 新增按钮入口与授权校验
  - `Tools/Analysis/DataPivot/DataPivotDockPane.cs` - 新增停靠窗格容器
  - `Tools/Analysis/DataPivot/DataPivotDockPaneView.xaml` - 新增工具界面
  - `Tools/Analysis/DataPivot/DataPivotDockPaneView.xaml.cs` - 新增视图初始化与加载逻辑
  - `Tools/Analysis/DataPivot/DataPivotDockPaneViewModel.cs` - 新增数据透视主流程、参数与日志
  - `Config.daml` - 新增按钮/停靠窗格注册并更新分组与面板名称

---

## Bug 修复

### MDB批量转GDB中文编码与性能修复 (2026-03-05)
- 修复中文要素集/要素图层名称读取与输出乱码问题，补充 OGR 原生字符串读取与图层创建编码处理。
- 修复字符串字段值复制到输出 GDB 时的乱码问题，新增字符串字段重写逻辑。
- 修复编码检测阶段频繁抛出 `System.Text.DecoderFallbackException` 导致转换变慢的问题，改为无异常的字节判定流程。
- 修复新增工具引入后的程序集特性重复编译错误（CS0579），避免 `AssemblyInfo` / `TargetFrameworkAttribute` 重复生成。
- 修复 AddIn 分发包未包含 `Install/gdal/**` 运行时目录的问题，新增构建输出追踪确保 GDAL native/data/share 文件打入 `.esriAddinX`。
- 修改的文件：
  - `Tools/DataProcessing/MdbBatchToGdb/OgrUtf8Interop.cs` - 编码读取/写入互操作与无异常判定
  - `Tools/DataProcessing/MdbBatchToGdb/GdalMdbToGdbConverter.cs` - 字符串字段重写与输出图层创建修复
  - `Tools/DataProcessing/MdbBatchToGdb/GdalMdbLayerInspector.cs` - 元数据字符串读取修复
  - `XIAOFUTools.csproj` - 关闭重复程序集特性自动生成

### 要素类转TXT面积与默认值修复 (2026-03-05)
- 修复“地块面积”输出单位不一致问题：当面积字段为空、无效、为0或负数时，自动使用几何面积换算为公顷并保留4位小数；当字段值疑似平方米时自动换算为公顷。
- 移除面积自动匹配字段 `Shape_Area`、`SHAPE_AREA`、`AREA`，避免直接输出平方米导致结果异常。
- 修复缺失字段兜底规则：地块编号缺失时按 `1,2,3...` 顺序编号；地块名称缺失时按 `地块1,地块2,地块3...` 自动生成。
- 调整编号映射，去除 `OBJECTID`、`FID` 作为“地块编号”候选，避免系统OID误用为业务编号。
- 修改的文件：
  - `Tools/Convert/FeatureToTxt/FeatureToTxtDockPaneViewModel.cs` - 面积字段匹配规则、面积兜底计算与单位换算、地块编号/名称默认生成逻辑。

### 资源路径与图片转PDF依赖修复 (2026-03-05)
- 修复项目资源清单中的路径错配：将 `星图-地形地图(注记)`、`星图-影像地图(注记)` 从 URL 编码文件名改为实际文件名，避免构建输出时漏拷贝。
- 移除不存在的 `Data/Excel模板/地类表.xlsx` 复制配置，消除无效资源项。
- `图片转PDF` 依赖从 `PdfSharpCore` 切换为 `PDFsharp-GDI`，并同步命名空间引用，保持现有功能与调用方式不变。
- 依赖漏洞扫描已通过：不再出现 `SixLabors.ImageSharp 1.0.4` 传递漏洞告警。
- 修改的文件：
  - `XIAOFUTools.csproj` - 修正资源路径、移除无效模板项、替换 PDF 依赖包
  - `Tools/Convert/ImagesToPdf/ImagesToPdfDockPaneViewModel.cs` - 更新 PdfSharp 命名空间引用


### 计算面积右键上下文图层传参修复 (2026-03-05)
- 修复“计算面积”从图层右键菜单打开时图层定位不稳定的问题，新增右键上下文图层数据选项传递（图层 URI + 图层名称）。
- 右键上下文优先通过 `ContextMenuDataContext` 获取目标图层，获取失败时回退到当前选中图层。
- DockPane 与 ViewModel 新增上下文参数透传，并在图层匹配时改为 URI 优先、名称兜底，避免同名图层误匹配。
- 修复可访问性不一致编译错误 CS0051：`AreaCalculatorDockPaneView.ApplyContextOptions(...)` 调整为 `internal`。
- 修改的文件：
  - `Tools/Analysis/AreaCalculator/AreaCalculatorButton.cs` - 右键上下文图层解析与参数封装。
  - `Tools/Analysis/AreaCalculator/AreaCalculatorDockPane.cs` - 新增上下文选项对象并透传到视图。
  - `Tools/Analysis/AreaCalculator/AreaCalculatorDockPaneView.xaml.cs` - 视图上下文入口与方法可见性修复。
  - `Tools/Analysis/AreaCalculator/AreaCalculatorDockPaneViewModel.cs` - URI 优先图层匹配与上下文应用。

### 要素图层转KML导出链路修复与字段标注校正 (2026-02-10)
- 将要素图层转 KML/KMZ 导出改为调用 ArcGIS Pro 内置 `Layer To KML`，解决自实现链路在部分坐标系、符号样式上的兼容问题。
- 分组导出改为直接基于源图层按字段筛选并导出，避免临时图层导致的颜色丢失、名称异常和工具校验失败问题。
- 标注层改为直接读取所选字段并按几何生成标注点（面优先面内点），同时同步校正 `Placemark` 名称与字段值一致。
- 优化导出性能：移除分组场景下的二次临时图层/临时要素重复转换，减少 GP 调用开销。
- 修改的文件:
  - `Tools/Convert/ExportToKml/ExportToKmlViewModel.cs` - 内置导出链路、分组筛选策略、字段标注生成与 KML 合并逻辑重构

### 数据透视临时表不存在问题修复 (2026-02-09)
- 修复 `in_memory` 临时表在连续 GP 调用中可能失效导致 `ERROR 000732` 的问题。
- 临时工作空间改为输出 GDB（或项目默认 GDB），并新增可用性校验与日志输出。
- 修复下拉框偶发显示对象类型名（代码名）的问题，统一回退显示为可读名称。
- 修改的文件:
  - `Tools/Analysis/DataPivot/DataPivotDockPaneViewModel.cs` - 临时工作空间策略、显示文本回退与错误防护

---

## 改进优化

### 属性表建库坐标系与模板增强 (2026-02-10)
- “属性表建库”工具新增图层坐标系设置项，支持在界面选择并应用到新建要素集/要素类；同时将“导出模板”按钮调整到底部帮助按钮旁，提升操作连贯性。
- 建库流程不再固定使用 WGS84，支持读取所选坐标系；并增强兼容性：字段类型与几何类型支持中文别名输入（如“文本/点/线/面/多点”等）。
- 两份建库模板（普通版/带要素集版）已优化：补充填写说明、字段类型中文解释、几何/字段类型下拉校验，以及约束条件 `M/C/O` 下拉与中文提示。
- 修改的文件:
  - `Tools/DataProcessing/DatabaseBuilder/DatabaseBuilderDockPaneView.xaml` - 新增坐标系选择区并调整底部按钮布局
  - `Tools/DataProcessing/DatabaseBuilder/DatabaseBuilderViewModel.cs` - 新增坐标系参数传递与中文类型兼容解析
  - `Data/Excel模板/建库模板.xls` - 增强说明与数据校验（含约束条件下拉）
  - `Data/Excel模板/建库模板-带要素集.xls` - 增强说明与数据校验（含约束条件下拉）

### 驱动制图界址点表与页面搜索优化 (2026-02-09)
- 界址点表新增“压缩总行数”参数，点数超阈值时压缩后总显示行数按设定值控制（并自动按每列行数分表）。
- 压缩时省略行位置优化为尽量落在表格可视中心区域，减少省略行偏上/偏下。
- 省略标记统一为 `•••`，并修正边长列错位场景：与省略行相邻的上下边长均显示 `•••`。
- 驱动制图主界面新增页面搜索能力，支持下拉切换按“页码”或“名称”实时过滤列表。
- 全选/反选操作改为对当前筛选结果生效，便于搜索后批量处理。
- 修改的文件:
  - `Tools/Convert/MapSeriesExport/MapSeriesSettingsWindow.xaml` - 新增压缩总行数输入项
  - `Tools/Convert/MapSeriesExport/MapSeriesSettingsWindow.xaml.cs` - 新增参数读取、保存与校验
  - `Tools/Convert/MapSeriesExport/MapSeriesExportDockPaneView.xaml` - 新增搜索框与搜索模式下拉
  - `Tools/Convert/MapSeriesExport/MapSeriesExportViewModel.cs` - 实现压缩省略、边长联动省略与页面过滤逻辑

### 数据透视工具图标更新 (2026-02-09)
- 新增数据透视专用 SVG 图标并转换生成 16/32 PNG 图标，替换原复用图标。
- 修改的文件:
  - `IconTools/Icons/DataPivot.svg` - 新增矢量图标源文件
  - `Images/DataPivot_16.png` - 新增小图标
  - `Images/DataPivot_32.png` - 新增大图标
  - `Config.daml` - 数据透视按钮图标路径改为专用图标
  - `XIAOFUTools.csproj` - 添加新图标内容清单

---

## 记录规范

### 格式模板

```markdown
### 功能名称 (YYYY-MM-DD)
- 做了什么
- 解决了什么问题
- 修改的文件:
  - `path/to/file1.cs` - 说明
  - `path/to/file2.xaml` - 说明
```

### 分类说明

| 分类 | 用途 |
|------|------|
| 新增功能 | 新开发的功能特性 |
| Bug 修复 | 修复的问题和错误 |
| 改进优化 | 性能优化、重构、文档更新 |
