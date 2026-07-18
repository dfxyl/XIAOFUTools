# XIAOFUTools 发布指南

## 版本准备

1. 在 `CHANGELOG.md` 将 `[Unreleased]` 内容归入新版本并填写发布日期。
2. 同步更新 `Config.daml`、`Shared/Configuration/VersionInfo.cs`、`version.json` 和关于窗口显示版本。
3. 确认目标平台仍为 ArcGIS Pro 3.7、.NET 10、Windows x64。

## 验证

```powershell
dotnet test tests/XIAOFUTools.Tests/XIAOFUTools.Tests.csproj `
  -p:ArcGISProInstallDir="D:\RUANJIAN\ArcGIS\Pro" `
  -p:Platform=x64 `
  -c Release `
  --filter "Category!=LiveNetwork"

dotnet build XIAOFUTools.sln `
  -p:ArcGISProInstallDir="D:\RUANJIAN\ArcGIS\Pro" `
  -p:Platform=x64 `
  -c Release
```

完成自动验证后，在 ArcGIS Pro 3.7 中检查：

- Add-in 安装、XIAOFU工具箱选项卡、GIS 工具口袋标签页和 DockPane 正常加载。
- 通用、编辑、分析、转换、数据整备、制图和系统功能各执行一个代表工具。
- AI 助手、Office 导出、影像下载、设置持久化和更新检查正常。
- GIS 工具口袋可添加工具箱、搜索工具、打开工具并在设置中显示或隐藏标签页。
- 旧版本设置与 AI 数据库能够直接升级使用。

发布包位于 `bin/x64/Release/net10.0-windows/XIAOFUTools.esriAddinX`。

## 发布

1. 提交发布变更并创建 `v<version>` 标签。
2. 推送主分支和标签。
3. 在发行页上传 `.esriAddinX`。
4. 将最终下载地址写入 `version.json` 并验证能够匿名下载。
5. 安装线上包进行一次干净环境验证。

## 发布检查清单

- 版本号和日期在所有位置一致。
- 默认测试、架构测试和 Release x64 构建通过。
- `.esriAddinX` 包含 Config.daml、Assets、样式、AI Web 文件和 Python 脚本。
- CHANGELOG 与发行说明内容一致。
- 下载地址、升级覆盖和回退包均已验证。
