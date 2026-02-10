# 未发布的更改

记录开发中的修改，发布新版本时将内容移入 [CHANGELOG.md](CHANGELOG.md)。

**最后更新**: 2026-02-10

---

## 新增功能

### 提取面扣岛工具（分析/计算组）(2026-02-10)
- 新增“提取面扣岛”工具，支持从输入面要素中提取所有扣洞并输出为面要素图层。
- 输出结果保留源图层属性；多个洞可按开关选择逐洞输出或按源要素合并为多部件输出。
- 输出路径增强：非 GDB 路径自动规范为 `.shp`，该规范化仅用于处理，不再单独写入日志。
- 修改的文件:
  - `Config.daml` - 新增按钮、停靠窗格与“分析/计算”组入口
  - `Tools/Analysis/ExtractPolygonHoles/ExtractPolygonHolesButton.cs` - 新增按钮入口与授权校验
  - `Tools/Analysis/ExtractPolygonHoles/ExtractPolygonHolesDockPane.cs` - 新增停靠窗格容器
  - `Tools/Analysis/ExtractPolygonHoles/ExtractPolygonHolesDockPaneView.xaml` - 新增工具界面与参数项
  - `Tools/Analysis/ExtractPolygonHoles/ExtractPolygonHolesDockPaneView.xaml.cs` - 新增视图初始化逻辑
  - `Tools/Analysis/ExtractPolygonHoles/ExtractPolygonHolesViewModel.cs` - 新增扣洞提取、属性复制、输出路径规范化与运行流程
  - `Tools/Analysis/ExtractPolygonHoles/RelayCommand.cs` - 新增命令实现

### 数据透视工具（分析/计算组）(2026-02-09)
- 新增“数据透视”工具，支持从地图中的要素图层/独立表选择输入数据。
- 支持多区域字段分组、透视字段展开、数值字段汇总，汇总方式支持：求和、计数、平均值、最大值、最小值、中位数、极差、标准差、方差。
- 新增默认命名规则：输出表名自动为 `TS_输入数据名称`，并在窗格加载时自动回到项目默认 GDB。
- 修改“计算”面板名称为“分析/计算”，并同步分组文案为“编辑/分析/计算工具”。
- 修改的文件:
  - `Tools/Analysis/DataPivot/DataPivotButton.cs` - 新增按钮入口与授权校验
  - `Tools/Analysis/DataPivot/DataPivotDockPane.cs` - 新增停靠窗格容器
  - `Tools/Analysis/DataPivot/DataPivotDockPaneView.xaml` - 新增工具界面
  - `Tools/Analysis/DataPivot/DataPivotDockPaneView.xaml.cs` - 新增视图初始化与加载逻辑
  - `Tools/Analysis/DataPivot/DataPivotDockPaneViewModel.cs` - 新增数据透视主流程、参数与日志
  - `Config.daml` - 新增按钮/停靠窗格注册并更新分组与面板名称

---

## Bug 修复

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
