# 未发布的更改

记录开发中的修改，发布新版本时将内容移入 [CHANGELOG.md](CHANGELOG.md)。

**最后更新**: 2026-02-09

---

## 新增功能

*暂无*

---

## Bug 修复

*暂无*

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
