# XIAOFUTools 版本发布指南

本文档说明如何发布新版本到 GitHub 仓库，并同步在线更新配置、更新日志与公告。

---

## 当前版本信息

- **版本号**: 1.2.6
- **更新日期**: 2026/03/09
- **最低 ArcGIS Pro 版本**: 3.6.0

---

## 1.2.5 后 GitHub 更新汇总（用于 1.2.6）

### 核心更新
- 新增 MDB批量转GDB、批量合并SHP、文档批量替换、属性表建SHP、SHP输字段表、提取面扣岛、数据透视 等工具。
- Ribbon 结构整理为 `通用 / 编辑 / 数据 / 制图 / 系统` 5 组，同步梳理 README、用户手册与开发规范文档。
- 属性表建库支持坐标系选择与模板增强；驱动制图补充页面搜索、界址点表压缩总行数与省略显示优化。
- 修复 MDB转GDB 中文编码与转换运行时打包、要素类转TXT面积与默认值、图片转PDF依赖、KML导出稳定性、数据透视临时表、计算面积右键上下文传参等问题。

### 对应提交
- `1fea20d` docs: 优化文档结构与格式
- `1847dfc` feat: refresh ArcGIS Pro tool icons
- `aa01b93` feat: 优化MDB转GDB元数据解析与写入流程
- `743edbb` feat: 完善MDB转GDB的ArcGIS写入链路
- `6f19fe2` feat: 新增MDB批量转GDB工具
- `7e78f35` fix: 修复要素类转TXT面积与默认值
- `14daf27` fix: 修正图片转PDF依赖与资源路径
- `1312d72` feat: 新增批量合并SHP工具并优化面积计算
- `60e52d8` feat: 新增文档批量替换工具并调整文档分组
- `949deb8` feat: 新增属性表建SHP与SHP输字段表工具
- `758470e` feat: 属性表建库支持坐标系配置并增强模板兼容性
- `83f8f78` feat: 新增提取面扣岛并修复KML导出稳定性
- `338b17f` feat: 新增数据透视工具并接入分析计算面板
- `8f0af66` feat: 增强图册导出与数据库组能力并补充发布素材

---

## version.json 配置

将以下内容保存为 `version.json` 并提交到 `main` 分支根目录：

```json
{
  "version": "1.2.6",
  "releaseDate": "2026-03-09",
  "minDesktopVersion": "3.6.0",
  "downloadUrl": "https://github.com/xiaofuX1/XIAOFUTools/releases/download/v1.2.6/XIAOFUTools.esriAddinX",
  "changelog": [
    "新增 MDB批量转GDB、批量合并SHP、文档批量替换、属性表建SHP、SHP输字段表、提取面扣岛、数据透视 等工具",
    "优化 Ribbon 分组、属性表建库模板、驱动制图页面搜索与界址点表显示体验",
    "修复 MDB转GDB 中文乱码与性能、KML 导出、图片转PDF 依赖、面积/默认值与右键上下文传参等问题"
  ],
  "notice": "欢迎使用XIAOFU工具箱，最新版本V1.2.6已发布，建议及时更新。该版本仅支持 ArcGIS Pro 3.6+。"
}
```

**重要**：确保 `downloadUrl` 与实际 GitHub Release 附件链接一致。

---

## 发布步骤

### 1. 同步版本与日志

修改以下文件中的版本号、日期和发布记录：

| 文件 | 位置 |
|------|------|
| `Common/VersionInfo.cs` | `CurrentVersion` 常量 |
| `Config.daml` | `AddInInfo` 的 `version` 属性和 `Date` 元素 |
| `Tools/User/About/AboutDialog.xaml` | 版本显示文本和更新摘要 |
| `CHANGELOG.md` | 新版本变更记录 |
| `UNRELEASED.md` | 清空已发布记录，恢复模板 |
| `AGENTS.md` | 项目概述中的当前版本 |
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
git add Common/VersionInfo.cs Config.daml Tools/User/About/AboutDialog.xaml CHANGELOG.md UNRELEASED.md RELEASE.md AGENTS.md version.json
git commit -m "chore: release v1.2.6"
git push origin main
```

### 4. 推送 Tag 与创建 GitHub Release

#### 4.1 网页方式
1. 访问 `https://github.com/xiaofuX1/XIAOFUTools/releases`
2. 点击 `Draft a new release`
3. 填写信息：
   - **Tag**: `v1.2.6`
   - **Release title**: `XIAOFU工具箱 v1.2.6`
   - **Description**: 粘贴本次更新内容
4. 上传 `XIAOFUTools.esriAddinX`
5. 点击 `Publish release`

#### 4.2 命令行方式（推荐）

```powershell
gh release create v1.2.6 "bin/Release/net8.0-windows/XIAOFUTools.esriAddinX" --title "XIAOFU工具箱 v1.2.6" --notes "详见 CHANGELOG.md"
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
XIAOFU工具箱 v1.2.6 更新发布

更新内容：
• 新增 MDB批量转GDB、批量合并SHP、文档批量替换、属性表建SHP、SHP输字段表、提取面扣岛、数据透视 等工具
• 优化 Ribbon 分组、属性表建库模板、驱动制图页面搜索与界址点表显示体验
• 修复 MDB转GDB 中文乱码与性能、KML 导出、图片转PDF 依赖、面积/默认值与右键上下文传参等问题

发布日期：2026-03-09
```

### 详细版

```
XIAOFU工具箱 v1.2.6 正式发布

更新内容：
1. 新增多项数据处理与文档工具
   - 覆盖 MDB批量转GDB、批量合并SHP、属性表建SHP、SHP输字段表、文档批量替换、提取面扣岛、数据透视
2. 体验与配置优化
   - Ribbon 分组重整，属性表建库与驱动制图交互进一步完善
3. 多链路稳定性修复
   - 修复 MDB 编码与运行时打包、KML 导出、图片转PDF 依赖、面积换算与右键上下文传参等问题

发布日期：2026年03月09日

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
- [ ] 更新日志已写入 `CHANGELOG.md`
- [ ] `UNRELEASED.md` 已清空并恢复模板
- [ ] 代码已通过编译测试
- [ ] `version.json` 内容与 Release 信息一致
- [ ] 插件文件已生成
- [ ] GitHub Release 已创建
- [ ] 下载链接已验证可用
- [ ] 公告已发布

---

**维护者**: XIAOFU
