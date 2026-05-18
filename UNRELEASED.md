# 未发布的更改

记录开发中的修改，发布新版本时将内容移入 [CHANGELOG.md](CHANGELOG.md)。

**最后更新**: 2026-05-18

---

## 新增功能

### 驱动制图交集表格 (2026-05-18)
- 驱动制图设置新增“交集表格设置”，支持按当前驱动红线与指定面图层相交计算并显示布局表格
- 支持选择相交图层、分类字段、面积单位、小数位、类别列宽、面积列宽、行高、放置角点和偏移
- 交集表格跟随地图系列页面切换和批量导出流程自动生成、清除，定位方式与界址点表一致
- 交集面积按当前红线总面积调平，未覆盖部分自动归入“其他”
- 修改的文件:
  - `Tools/Convert/MapSeriesExport/MapSeriesExportViewModel.cs` - 接入当前页交集计算、布局表格生成、切页/导出清理流程和帮助说明
  - `Tools/Convert/MapSeriesExport/MapSeriesSettingsWindow.xaml` - 新增交集表格设置界面
  - `Tools/Convert/MapSeriesExport/MapSeriesSettingsWindow.xaml.cs` - 新增交集图层/字段加载、设置保存和输入校验
  - `Tools/Convert/MapSeriesExport/MapSeriesIntersectTableBuilder.cs` - 新增交集表格行构建、面积调平和单位转换逻辑
  - `XIAOFUTools.Tests/MapSeriesExport/MapSeriesIntersectTableBuilderTests.cs` - 覆盖交集表格构建核心规则
  - `XIAOFUTools.Tests/XIAOFUTools.Tests.csproj` - 引入交集表格构建器测试目标

## Bug 修复

### 要素图层分组导出KML/KMZ (2026-05-18)
- 修复勾选“生成文字标注层”后，“标注字段”下拉框鼠标点击不展开、只能通过键盘方向键切换的问题
- 修复字段列表刷新后已选择的标注字段被重置的问题，保留仍然存在的用户选择
- 修改的文件:
  - `Tools/Convert/ExportToKml/ExportToKmlDockPaneView.xaml` - 为标注字段下拉框补齐统一样式、双向绑定和鼠标展开事件
  - `Tools/Convert/ExportToKml/ExportToKmlDockPaneView.xaml.cs` - 点击标注字段区域时主动展开下拉框
  - `Tools/Convert/ExportToKml/ExportToKmlViewModel.cs` - 字段刷新时保留有效的分组字段和标注字段选择
  - `Tools/Convert/ExportToKml/ExportToKmlFieldSelection.cs` - 抽取字段选择保留逻辑
  - `XIAOFUTools.Tests/ExportToKml/ExportToKmlDockPaneViewXamlTests.cs` - 覆盖标注字段下拉框样式、绑定与鼠标事件配置
  - `XIAOFUTools.Tests/ExportToKml/ExportToKmlFieldSelectionTests.cs` - 覆盖字段刷新时保留用户选择

## 改进优化

### 驱动制图交集表格“其他”显示优化 (2026-05-18)
- “其他”面积按保留小数位显示为 0 时不再生成表格行
- “其他”只占一个最小显示单位时不再单独显示，自动并入非“其他”分类里保留位数下一位余数最大的行
- 修改的文件:
  - `Tools/Convert/MapSeriesExport/MapSeriesIntersectTableBuilder.cs` - 优化极小“其他”面积的显示和并入规则
  - `XIAOFUTools.Tests/MapSeriesExport/MapSeriesIntersectTableBuilderTests.cs` - 覆盖“其他”为 0 不显示和 0.01 并入分类的规则

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
