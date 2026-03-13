# 未发布的更改

记录开发中的修改，发布新版本时将内容移入 [CHANGELOG.md](CHANGELOG.md)。

**最后更新**: 2026-03-13

---

## 新增功能

### 图幅赋值与查询工具 (2026-03-13)
- 新增 `大比例图幅赋值` 与 `小比例图幅赋值` 两个工具，支持选择面图层和目标文本字段，将命中的多个图幅编号以 `、` 连接后写入图斑字段。
- 新增地图查询流程，支持连续查询图幅、在窗格内显示当前查询状态与查询结果，并可通过 `退出查询` 主动取消。
- 抽取公共图幅计算、空间命中判断、赋值比对与查询状态管理逻辑，补充纯算法测试。
- 修改的文件:
  - `Config.daml` - 注册大/小比例图幅赋值与查询工具，并补充按钮与提示文案。
  - `Common/LargeScaleMapSheetCalculator.cs` - 抽取大比例图幅计算逻辑。
  - `Common/SmallScaleMapSheetCalculator.cs` - 抽取小比例图幅计算逻辑。
  - `Common/MapSheetGeometryService.cs` - 统一图幅命中计算与查询结果生成。
  - `Common/MapSheetAssignmentUtils.cs` - 增加字段值比对，跳过未变化记录。
  - `Common/MapSheetIdentifySession.cs` - 管理查询模式状态与启停逻辑。
  - `Tools/DataProcessing/MapSheetsLargeAssign/*` - 新增大比例图幅赋值窗格、查询工具与交互逻辑。
  - `Tools/DataProcessing/MapSheetsSmallAssign/*` - 新增小比例图幅赋值窗格、查询工具与交互逻辑。
  - `XIAOFUTools.Tests/*` - 新增图幅计算、赋值比较与查询状态测试。

### 特殊坐标转换工具升级 (2026-03-09)
- 将工具界面重构为 `单层`、`批量`、`gdb` 三种模式，统一进度、日志、帮助与停止控制。
- 新增批量 SHP 扫描与转换，支持遍历子文件夹、勾选、全选、反选、清空选择，以及输出到源文件夹或指定目录。
- 新增批量 GDB 转换流程，按库生成新的结果 GDB，并尽量保留要素数据集结构。
- 修改的文件:
  - `Tools/Convert/SpecialCoordinateTransform/SpecialCoordinateTransformDockPaneView.xaml` - 重构界面为三标签页，补充批量与 GDB 操作入口。
  - `Tools/Convert/SpecialCoordinateTransform/SpecialCoordinateTransformDockPaneView.xaml.cs` - 增加忙碌状态反转转换器，并在加载时统一刷新图层。
  - `Tools/Convert/SpecialCoordinateTransform/SpecialCoordinateTransformDockPaneViewModel.cs` - 拆分视图模型入口，补充模式状态与命令定义。
  - `Tools/Convert/SpecialCoordinateTransform/SpecialCoordinateTransformDockPaneViewModel.Batch.cs` - 实现 SHP/GDB 批量扫描、选择控制、帮助与取消逻辑。
  - `Tools/Convert/SpecialCoordinateTransform/SpecialCoordinateTransformDockPaneViewModel.State.cs` - 管理模式状态、扫描状态、可执行条件与图层刷新。
  - `Tools/Convert/SpecialCoordinateTransform/SpecialCoordinateTransformDockPaneViewModel.Support.cs` - 补充批量转换计划与输出路径辅助逻辑。
  - `Tools/Convert/SpecialCoordinateTransform/SpecialCoordinateTransformDockPaneViewModel.Transform.cs` - 拆分单层、批量 SHP、批量 GDB 三类转换执行流程。

## Bug 修复

- 暂无

## 改进优化

### 图幅赋值与查询工具 (2026-03-13)
- 优化地图查询交互，查询时仅闪烁命中的图幅边框，不在地图上残留常驻标记。
- 修复查询模式重复启动后无法连续使用的问题，改为可连续查询并可显式退出。
- 避免窗格重复订阅查询结果回调，减少重复日志与状态错乱。
- 优化批量赋值流程，仅对字段值发生变化的图斑提交编辑，减少不必要写入。

### 特殊坐标转换工具升级 (2026-03-09)
- 优化 GDB 要素类打开与输出创建逻辑，支持处理要素数据集内的要素类。
- 优化字段映射与写入逻辑，跳过 `GlobalID`、`Shape_*` 等不可编辑或系统字段，减少批量输出失败。
- 使用 `InsertCursor` 分批写入并按间隔刷新进度与日志，提升大批量要素转换时的稳定性和性能。
- 避免在没有可用地图要素图层时重复写入同一条日志，减少界面噪音。
- 修改的文件:
  - `Tools/Convert/SpecialCoordinateTransform/SpecialCoordinateTransformDockPaneViewModel.State.cs` - 限制“无可用图层”日志重复输出。
  - `Tools/Convert/SpecialCoordinateTransform/SpecialCoordinateTransformDockPaneViewModel.TransformHelpers.cs` - 优化 GDB 路径解析、数据集创建、字段过滤与批量写入。

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
