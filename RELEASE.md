# XIAOFUTools 版本发布指南

本文档说明如何发布新版本到 GitHub 仓库并推送更新通知。

---

## 当前版本信息

- **版本号**: 1.2.5
- **更新日期**: 2026/02/09
- **最低 ArcGIS Pro 版本**: 3.6.0

---

## 1.2.4 后 GitHub 更新汇总（用于 1.2.5）

### 核心更新
- AI 助手完成分层重构，新增应用编排层、工具请求解析层、提示词管理与数据存储抽象
- AI 助手新增 GIS 工具链：图层列表、图层查询、图层缓冲、图层裁剪、字段画像、图层结构、压盖汇总、选中摘要、项目快照、联网抓取
- 分析/转换/数据处理/编辑等模块进行了多项功能与 UI 交互优化
- 图标与样式资源批量更新，统一工具视觉风格
- 修复若干bug

### 对应提交
- `e0aaf99` feat: 新增多项工具功能与资源更新
- `b323783` feat: 多模块功能优化与UI改进
- `cd37064` feat: 重构AI助手架构并扩展GIS智能工具能力

---

## version.json 配置

将以下内容保存为 `version.json` 并提交到 `main` 分支根目录：

```json
{
  "version": "1.2.5",
  "releaseDate": "2026-02-09",
  "minDesktopVersion": "3.6.0",
  "downloadUrl": "https://github.com/xiaofuX1/XIAOFUTools/releases/download/v1.2.5/XIAOFUTools.esriAddinX",
  "changelog": [
    "AI助手完成分层重构并扩展GIS智能工具链",
    "多模块功能与UI交互优化，提升批量处理与转换体验",
    "修复若干bug"
  ],
  "notice": "欢迎使用XIAOFU工具箱，最新版本V1.2.5已发布，建议及时更新。该版本仅支持 ArcGIS Pro 3.6+。"
}
```

**重要**：确保 `downloadUrl` 与实际 gitee Release 附件链接一致。

---

## 发布步骤

### 1. 更新版本号

修改以下文件中的版本号与日期：

| 文件 | 位置 |
|------|------|
| `Common/VersionInfo.cs` | `CurrentVersion` 常量 |
| `Config.daml` | `AddInInfo` 的 `version` 属性和 `Date` 元素 |
| `Tools/User/About/AboutDialog.xaml` | 版本显示文本和更新内容 |
| `CHANGELOG.md` | 新版本变更记录 |
| `version.json` | 在线更新配置 |

### 2. 构建发布版本

```powershell
cd d:\Development\XIAOFUTools\XIAOFUTools
dotnet build -c Release
```

编译后的插件文件位于：`bin\Release\net8.0-windows\XIAOFUTools.esriAddinX`

### 3. 推送到 GitHub（代码）

```powershell
git status
git add .
git commit -m "chore: release v1.2.5"
git push origin main
```

### 4. 创建 GitHub Release

#### 4.1 网页方式
1. 访问 `https://github.com/xiaofuX1/XIAOFUTools/releases`
2. 点击 `Draft a new release`
3. 填写信息：
   - **Tag**: `v1.2.5`
   - **Release title**: `XIAOFU工具箱 v1.2.5`
   - **Description**: 粘贴本次更新内容
4. 上传 `XIAOFUTools.esriAddinX`
5. 点击 `Publish release`

#### 4.2 命令行方式（gh）

```powershell
gh release create v1.2.5 "bin/Release/net8.0-windows/XIAOFUTools.esriAddinX" --title "XIAOFU工具箱 v1.2.5" --notes "详见 CHANGELOG.md"
```

### 5. 发布公告

将更新内容发布到：
- QQ群：967758553
- 哔哩哔哩动态
- 其他相关平台

---

## 公告模板

### 简洁版

```
XIAOFU工具箱 v1.2.5 更新发布

更新内容：
• AI助手完成分层重构并扩展GIS智能工具链
• 多模块功能与UI交互优化，批量处理与转换体验提升
• 修复若干bug

发布日期：2026-02-09
```

### 详细版

```
XIAOFU工具箱 v1.2.5 正式发布

更新内容：
1. AI助手架构重构并扩展GIS智能工具能力
   - 新增图层查询、缓冲、裁剪、压盖汇总、项目快照等工具
   - 引入工具请求解析与应用编排层，提升多轮对话与工具调用稳定性
2. 多模块功能与UI交互优化
   - 覆盖分析、转换、数据处理、编辑等核心工具链
3. 发布流程升级
   - 更新日志与版本推送方法切换至 GitHub

发布日期：2026年02月09日

下载方式：
- 方式一：通过工具箱内置“检查更新”功能自动更新
- 方式二：访问 GitHub Releases 手动下载安装包

重要提示：
- 该版本仅支持 ArcGIS Pro 3.6+
- 在使用任何工具前，请务必备份原始数据
```

---

## 发布检查清单

- [ ] 版本号已在所有相关文件中更新
- [ ] 更新内容描述准确清晰
- [ ] 代码已通过编译测试
- [ ] `version.json` 内容与 Release 信息一致
- [ ] 插件文件已生成
- [ ] GitHub Release 已创建
- [ ] 下载链接已验证可用
- [ ] 公告已发布

---

**维护者**: XIAOFU
