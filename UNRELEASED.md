# 未发布的更改

记录开发中的修改，发布新版本时将内容移入 [CHANGELOG.md](CHANGELOG.md)。

**最后更新**: 2026-02-04

---

## 新增功能

*暂无*

---

## Bug 修复

*暂无*

---

## 改进优化

### 驱动制图页面加载性能优化 (2026-02-05)
- 优化地图系列页面列表加载速度，解决几千个要素加载时卡死的问题
- 优化1：直接从索引图层批量查询所有页面名称，避免逐页调用 `SetCurrentPageNumber` 切换
- 优化2：使用 `PostfixClause` ORDER BY 排序，确保与地图系列的实际排序一致
- 优化3：将 `MapSeriesPages` 改为可替换属性，在后台线程构建完整集合后一次性替换，避免逐个 Add 触发 UI 刷新
- 优化4：ListBox 启用 UI 虚拟化（VirtualizingStackPanel），避免一次性创建几千个 ListBoxItem 导致卡死
- 优化5：完全移除回退遍历逻辑，查询失败时直接用页码显示
- 添加调试日志输出，便于排查性能问题
- 修改的文件:
  - `Tools/Convert/MapSeriesExport/MapSeriesExportViewModel.cs` - 重写 LoadMapSeriesPages 方法
  - `Tools/Convert/MapSeriesExport/MapSeriesExportDockPaneView.xaml` - ListBox 启用虚拟化

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
