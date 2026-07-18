# 历史影像拖拽加载设计

## 目标

为 `历史影像` 工具增加“从版本列表拖拽到地图视图加载”的交互，同时保留现有右键“添加到地图”能力，并让两者复用同一套加图逻辑。

## 现状

- 版本树界面位于 `Tools/Common/HistoricalImagery/HistoricalImageryDockPaneView.xaml`。
- 右键菜单“添加到地图”在 `Tools/Common/HistoricalImagery/HistoricalImageryDockPaneView.xaml:131` 触发。
- 实际加图逻辑直接写在 `Tools/Common/HistoricalImagery/HistoricalImageryDockPaneViewModel.cs` 的 `AddSelectedLayer()` 中。
- 当前没有拖拽相关处理，也没有针对历史影像窗格的单元测试。

## 设计

### 1. 抽离统一的图层加载请求

新增一个纯逻辑请求构造器，用于把 `WaybackVersion` 转换为统一的图层加载请求：

- 校验版本是否存在、URL 是否为空。
- 统一生成图层名称 `Wayback {ReleaseDate}`。
- 输出标准化的 `Uri`，供右键加载和拖拽加载共用。

这样可以先为核心命名与校验行为补测试，再让 UI 与 ArcGIS 依赖层调用同一请求对象。

### 2. 抽离地图加载服务

新增 `HistoricalImageryMapLoadService`：

- 接收统一请求对象。
- 在 `QueuedTask.Run` 中调用 `LayerFactory.Instance.CreateLayer`。
- 成功后保持现有行为：将图层尽量移动到底部。
- 返回成功/失败结果，供右键命令与拖拽入口决定是否弹提示。

### 3. 为版本树接入 ArcGIS Pro 拖拽源

参考项目里 `QuickAddData` 的做法，为历史影像 `TreeView` 接入 `ArcGIS.Desktop.Framework.DragDrop`：

- 仅叶子节点允许开始拖拽。
- 拖拽源处理器从当前版本构造 ArcGIS 可识别的拖拽数据。
- 若 ArcGIS 原生拖放数据构造失败，则阻止拖拽开始，避免出现“能拖但落不到地图”的假交互。

### 4. 右键与拖拽共用同一套构造逻辑

- 右键菜单仍保留，改为调用统一请求构造器 + 地图加载服务。
- 拖拽也使用同一请求构造器，保证名称、URL 校验、错误提示一致。

## 异常处理

- 未选中版本、版本 URL 为空、当前没有活动地图时，不执行加图。
- 右键命令保持当前弹窗提示风格。
- 拖拽场景不主动弹成功提示，避免频繁打断；失败时仅阻止拖拽开始或在显式加载时提示。

## 测试

为纯逻辑请求构造器补单元测试：

- 有效版本可生成标准图层名称与 `Uri`。
- 缺少 URL 时返回失败。
- 缺少日期时回退到版本标题或版本号生成名称。

ArcGIS Pro 地图拖放本身属于宿主集成交互，使用本地 `dotnet test` 验证纯逻辑，再用 `dotnet build` 验证编译通过。
