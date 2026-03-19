# XIAOFUTools 版本发布指南

本文档说明如何发布新版本到 Gitee 仓库，并同步在线更新配置、更新日志与公告。

---

## 当前版本信息

- **版本号**: 1.2.7
- **更新日期**: 2026/03/19
- **最低 ArcGIS Pro 版本**: 3.6.0
- **版本清单地址**: `https://gitee.com/XFTools/xiaofutools/raw/master/version.json`
- **插件下载地址**: `https://gitee.com/XFTools/xiaofutools/releases/download/V1.2.7/XIAOFUTools.esriAddinX`

---

## 1.2.6 后 Gitee 更新汇总（用于 1.2.7）

### 核心更新
- 新增 `历史影像下载`、`互联网切片下载`、`大比例图幅赋值`、`小比例图幅赋值`、`快捷添加数据` 等功能。
- `历史影像` 与 `历史影像下载` 统一为 Wayback 真实变化判定逻辑，并补充图层来源解析、帮助说明与拖拽加载体验。
- `特殊坐标转换` 升级为 `单层 / 批量 / gdb` 三种模式，增强批量 SHP/GDB 转换、日志、进度与帮助流程。
- `MDB批量转GDB` 切换为 ArcGIS Pro Python 转换链路，优化要素集识别、元数据兼容与中文解析兜底。
- 修复 WMTS/天地图兼容、临时文件句柄释放、框选工具状态恢复、图幅连续查询与部分日志布局问题。

### 对应提交
- `f82eb50` refactor raster export and MDB conversion pipeline
- `2165596` feat: add drag loading for quick data and historical imagery
- `f3fda8f` fix: stop internet tile downloads with no coverage
- `9280fbd` feat: add internet tile download and relax tile blocking
- `35b1209` feat: 为历史影像工具添加帮助说明
- `187bd04` fix: 修复部分工具日志框拉伸布局
- `e823396` feat: improve historical imagery download fallback
- `ca2636e` feat: improve historical imagery metadata source selection
- `5e25add` feat: add historical imagery download and wayback change detection
- `0791598` 更新项目文件

---

## version.json 配置

将以下内容保存为 `version.json` 并提交到 Gitee `master` 分支根目录：

```json
{
  "version": "1.2.7",
  "releaseDate": "2026-03-19",
  "minDesktopVersion": "3.6.0",
  "downloadUrl": "https://gitee.com/XFTools/xiaofutools/releases/download/V1.2.7/XIAOFUTools.esriAddinX",
  "changelog": [
    "新增 历史影像下载、互联网切片下载、大/小比例图幅赋值与查询、快捷添加数据 等工具与面板",
    "升级 特殊坐标转换、MDB批量转GDB、Wayback 历史判定与拖拽加载交互，增强批量处理与兼容性",
    "修复 WMTS/天地图兼容、临时文件占用、框选工具状态恢复与图幅连续查询等问题"
  ],
  "notice": "欢迎使用XIAOFU工具箱，最新版本V1.2.7已发布，建议及时更新。该版本仅支持 ArcGIS Pro 3.6+。"
}
```

**重要**：确保 `downloadUrl` 与实际 Gitee Release 附件链接一致，`Tools/User/PluginUpdate/UpdateChecker.cs` 中的 `VersionUrl` 也已同步到 Gitee 原始文件地址。

---

## 发布步骤

### 1. 同步版本与日志

修改以下文件中的版本号、日期和发布记录：

| 文件 | 位置 |
|------|------|
| `Common/VersionInfo.cs` | `CurrentVersion` 常量 |
| `Config.daml` | `AddInInfo` 的 `version` 属性和 `Date` 元素 |
| `Tools/User/About/AboutDialog.xaml` | 版本显示文本和更新摘要 |
| `Tools/User/PluginUpdate/UpdateChecker.cs` | 在线版本清单地址 |
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

### 3. 推送代码到 Gitee

首次推送如未配置 `gitee` 远端，可先执行：

```powershell
git remote add gitee https://gitee.com/XFTools/xiaofutools.git
```

正式推送命令：

```powershell
git status
git add Common/VersionInfo.cs Config.daml Tools/User/About/AboutDialog.xaml Tools/User/PluginUpdate/UpdateChecker.cs CHANGELOG.md UNRELEASED.md RELEASE.md AGENTS.md version.json
git commit -m "chore: release v1.2.7"
git push gitee HEAD:master
```

### 4. 推送 Tag 并创建 Gitee Release

```powershell
git tag v1.2.7
git push gitee v1.2.7
```

然后在 Gitee 项目页面创建发行版并上传 `XIAOFUTools.esriAddinX`：

1. 打开 `https://gitee.com/XFTools/xiaofutools/releases`
2. 创建 `V1.2.7` 发行版
3. 标题填写 `XIAOFU工具箱 v1.2.7`
4. 粘贴本次更新摘要并上传 `bin\Release\net8.0-windows\XIAOFUTools.esriAddinX`
5. 发布后确认下载地址为：`https://gitee.com/XFTools/xiaofutools/releases/download/V1.2.7/XIAOFUTools.esriAddinX`

### 5. 验证在线更新与下载链接

```powershell
curl.exe -I -L "https://gitee.com/XFTools/xiaofutools/raw/master/version.json"
curl.exe -I -L "https://gitee.com/XFTools/xiaofutools/releases/download/V1.2.7/XIAOFUTools.esriAddinX"
```

### 6. 发布公告

将更新内容发布到：
- QQ群：967758553
- 哔哩哔哩动态
- 其他相关平台

---

## 公告模板

### 简洁版

```
XIAOFU工具箱 v1.2.7 更新发布

更新内容：
• 新增 历史影像下载、互联网切片下载、大/小比例图幅赋值与查询、快捷添加数据 等功能
• 升级 特殊坐标转换、MDB批量转GDB、Wayback 历史判定与拖拽加载交互
• 修复 WMTS/天地图兼容、临时文件占用、框选工具状态恢复与连续查询问题

发布日期：2026-03-19
下载地址：https://gitee.com/XFTools/xiaofutools/releases/download/V1.2.7/XIAOFUTools.esriAddinX
```

### 详细版

```
XIAOFU工具箱 v1.2.7 正式发布

更新内容：
1. 新增影像下载与图幅处理能力
   - 覆盖 历史影像下载、互联网切片下载、大比例图幅赋值、小比例图幅赋值 与 快捷添加数据 面板
2. 批量处理链路升级
   - 特殊坐标转换新增 单层 / 批量 / gdb 模式，MDB批量转GDB 切换为 ArcGIS Pro Python 引擎
3. 多链路稳定性修复
   - 修复 WMTS/天地图兼容、临时文件锁定、框选工具状态恢复、图幅连续查询等问题

发布日期：2026年03月19日

下载方式：
- 方式一：通过工具箱内置“检查更新”功能自动更新
- 方式二：访问 Gitee Releases 手动下载安装包

下载地址：
- https://gitee.com/XFTools/xiaofutools/releases/download/V1.2.7/XIAOFUTools.esriAddinX

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
- [ ] `UpdateChecker` 已指向 Gitee 原始 `version.json`
- [ ] 插件文件已生成
- [ ] Gitee Release 已创建
- [ ] 下载链接已验证可用
- [ ] 公告已发布

---

**维护者**: XIAOFU
