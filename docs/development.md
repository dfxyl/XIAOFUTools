# XIAOFUTools 开发指南

## 环境

- Windows x64
- ArcGIS Pro 3.7
- .NET SDK 10
- Visual Studio 2022，安装 ArcGIS Pro SDK 扩展

标准安装位置会被自动识别。非标准位置使用环境变量或构建参数指定：

```powershell
dotnet build XIAOFUTools.sln -c Debug -p:Platform=x64 -p:ArcGISProInstallDir="D:\RUANJIAN\ArcGIS\Pro"
```

## 新增功能

1. 在对应 `Features/<Domain>/<Feature>` 创建 Button、DockPane、View 和 ViewModel。
2. 复用 `Shared/Mvvm` 命令和操作状态；不要新增功能私有 `RelayCommand`。
3. 将纯规则放入 Core，将流程编排放入 Application，将 ArcGIS/HTTP/Office/SQLite/文件调用放入 Infrastructure。
4. 在 `Config.daml` 注册按钮、工具和 DockPane；ID 使用 `XIAOFUTools_<Feature><Kind>`，已有 ID 永不改名。
5. 16/32 像素图标放入 `Assets/Images`，无需手工修改项目文件。
6. 为纯规则增加单元测试；外部环境测试使用 `Category=Integration`，真实网络测试使用 `Category=LiveNetwork`。

## MVVM 与界面

- ViewModel 使用 `ObservableObject`、`RelayCommand`、`AsyncRelayCommand` 和 `OperationViewModelBase`。
- 对话框、文件选择、剪贴板、外部进程和 ArcGIS 进度窗口通过 Shared 接口调用；功能专属设置/结果窗口使用本功能的 Presentation 服务。
- UI 状态切换使用 `PresentationServices.UiThread`；ViewModel 不得直接调用 `Application.Current`、`Dispatcher`、`File/Directory` 或构造窗口。
- code-behind 只处理 WPF 生命周期、拖放、WebView 初始化和无法声明绑定的控件事件。
- 公共样式统一引用 `Shared/Presentation/Styles/ControlStyles.xaml`。

## ArcGIS Pro 安装路径

项目默认探测标准安装目录。非标准安装目录可通过环境变量、MSBuild 参数或本机 `XIAOFUTools.local.props` 提供；该文件已被 Git 忽略，不得提交机器专属路径。

```xml
<Project>
  <PropertyGroup>
    <ArcGISProInstallDir>D:\RUANJIAN\ArcGIS\Pro</ArcGISProInstallDir>
  </PropertyGroup>
</Project>
```

命令行 `-p:ArcGISProInstallDir=...` 优先于本机配置，可用于 CI 或临时切换 SDK 版本。

## 验证命令

```powershell
# 默认单元和架构测试，不访问真实网络或外部宿主
dotnet test tests/XIAOFUTools.Tests/XIAOFUTools.Tests.csproj -p:Platform=x64

# 本地文件、SQLite、Office 或 ArcGIS 环境验证
dotnet test tests/XIAOFUTools.Tests/XIAOFUTools.Tests.csproj -p:Platform=x64 --filter "Category=Integration"

# 显式联网验证
dotnet test tests/XIAOFUTools.Tests/XIAOFUTools.Tests.csproj -p:Platform=x64 --filter "Category=LiveNetwork"

# 发布构建
dotnet build XIAOFUTools.sln -c Release -p:Platform=x64 -p:ArcGISProInstallDir="D:\RUANJIAN\ArcGIS\Pro"
```

发布前必须确认默认测试通过、`.esriAddinX` 已生成、DAML/资源架构测试通过，并在 ArcGIS Pro 3.7 中完成各业务域冒烟验证。
