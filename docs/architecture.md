# XIAOFUTools 架构

## 架构目标

XIAOFUTools 使用单一生产程序集。ArcGIS Pro Add-in、WPF 界面和全部功能由 `XIAOFUTools.csproj` 构建，测试保留为独立验证工程。目录采用功能优先的模块化单体结构。

## 目录职责

- `App`：ArcGIS Pro 模块生命周期、启动检查和应用级组合。
- `Shared`：两个以上功能共同使用的基础设施。该目录不得引用 `Features`。
- `Features`：按 `General、Analysis、Conversion、DataManagement、Editing、Cartography、Custom、User` 业务域组织功能。
- `Assets`：运行时图标、预设图层、符号库和 Excel 模板。
- `tools`：开发期脚本和图标源文件，不承载运行时业务代码。
- `tests`：单元、集成、联网和架构测试。

小功能保持 Button、DockPane、View、ViewModel 同目录。超过八个文件或同时包含网络、数据库、Office、复杂算法的功能，使用 `Core / Application / Infrastructure / Presentation` 分层。

## 依赖方向

```text
App -> Features -> Shared
Presentation -> Application -> Core
Infrastructure -> Core
Shared -X-> Features
```

- 功能之间不得引用对方的 ViewModel、窗口或基础设施实现。
- 跨功能契约只有被两个以上功能使用时才能进入 `Shared`。
- ViewModel 通过服务接口访问对话框、文件选择、剪贴板、外部进程、进度窗口和功能专属设置/结果窗口；不得直接构造 WPF/ArcGIS 窗口。
- ViewModel 的 UI 状态更新通过 `PresentationServices.UiThread` 调度，文件系统访问通过功能的 Infrastructure 或 Shared IO 服务完成。
- HTTP、SQLite、Office、文件系统和 ArcGIS SDK 具体调用属于 Infrastructure 或 Shared 适配层。

## ArcGIS 线程规则

- ArcGIS Core/Mapping API 必须在 `QueuedTask` 或 `IArcGisTaskRunner` 中调用。
- 不得让 `Row、FeatureClass、Geodatabase、GeometryEngine` 相关可释放对象跨越任务边界。
- WPF 属性更新、窗口和集合变更回到 UI 线程执行。
- 长任务使用取消令牌、进度报告和统一异常处理。

## 稳定接口

重构不得改变 DAML `id/refID`、DockPane ID、设置 JSON 字段、SQLite 表结构、用户数据目录和导出文件格式。`Config.daml` 的 `className` 可以随内部命名空间调整，但必须由架构测试验证。

## 质量门槛

- ViewModel 原则上不超过 500 行，code-behind 不超过 300 行，其他生产 C# 文件不超过 800 行。
- `RelayCommand` 只允许在 `Shared/Mvvm` 定义。
- 架构测试禁止 ViewModel 直接访问 `Application.Current`、`Dispatcher`、`File/Directory`、进度窗口或自定义窗口。
- 默认测试禁止访问真实网络。
- 所有 DAML 内部引用、类名和本地资源必须通过架构测试。
