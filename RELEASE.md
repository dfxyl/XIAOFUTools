# XIAOFUTools 版本发布指南

本文档说明如何发布新版本到 Gitee 仓库并推送更新通知。

---

## 当前版本信息

- **版本号**: 1.2.2
- **更新日期**: 2025/12/26
- **最低 ArcGIS Pro 版本**: 3.6.0

---

## version.json 配置

将以下内容保存为 `version.json` 并上传到 Gitee 仓库的 master 分支根目录：

```json
{
  "version": "1.2.2",
  "releaseDate": "2025-12-26",
  "minDesktopVersion": "3.6.0",
  "downloadUrl": "https://gitee.com/XFTools/xiaofutools/releases/download/v1.2.2/XIAOFUTools.esriAddinX",
  "changelog": [
    "新增多图层压盖汇总，支持多压盖图层交集面积统计与导出",
    "新增地图生成界址点线，支持布局图形图层生成界址点/线/标注",
    "交集汇总表工具优化，支持多字段统计与Excel导出",
    "修复TXT转SHP"保存在txt源路径"选项报错",
    "该版本只支持3.6以上请更新",
    "非3.6不要更新",
    "ArcGIS Pro 3.6主程序-乐云盘：https://lecloud.lenovo.com/share/5kefSbRSLTw3KZNM8"
  ],
  "notice": "欢迎使用XIAOFU工具箱，最新版本V1.2.2已发布，建议及时更新。该版本只支持3.6以上请更新，非3.6不要更新。"
}
```

**重要**：确保 `downloadUrl` 链接与实际 Gitee Release 下载地址一致！

---

## 发布步骤

### 1. 更新版本号

修改以下文件中的版本号：

| 文件 | 位置 |
|------|------|
| `Common/VersionInfo.cs` | `CurrentVersion` 常量 |
| `Config.daml` | `AddInInfo` 的 `version` 属性和 `Date` 元素 |
| `Tools/User/About/AboutDialog.xaml` | 版本显示文本和更新内容 |

### 2. 构建发布版本

```powershell
cd d:\Development\XIAOFUTools\XIAOFUTools
dotnet build -c Release
```

编译后的插件文件位于：`bin\Release\net8.0-windows\XIAOFUTools.esriAddinX`

### 3. 创建 version.json

根据上方模板创建或更新 `version.json` 文件。

### 4. 上传到 Gitee

#### 4.1 上传 version.json
将 `version.json` 上传到 Gitee 仓库 master 分支根目录。

#### 4.2 创建 Release
1. 访问 https://gitee.com/XFTools/xiaofutools/releases
2. 点击"创建新的发布"
3. 填写信息：
   - **标签名称**: v1.2.2
   - **标题**: XIAOFU工具箱 v1.2.2
   - **发布说明**: 复制更新内容
4. 上传 `XIAOFUTools.esriAddinX` 文件
5. 点击"发布"

#### 4.3 确认下载链接
发布后复制实际下载链接，确保 `version.json` 中的 `downloadUrl` 一致。

### 5. 发布公告

将更新内容发布到：
- QQ群：967758553
- 哔哩哔哩动态
- 其他相关平台

---

## 公告模板

### 简洁版

```
XIAOFU工具箱 v1.2.2 更新发布

更新内容：
• 新增多图层压盖汇总，支持多压盖图层交集面积统计与导出
• 新增地图生成界址点线，支持布局图形图层生成界址点/线/标注
• 交集汇总表工具优化，支持多字段统计与Excel导出
• 修复TXT转SHP"保存在txt源路径"选项报错

发布日期：2025-12-26
```

### 详细版

```
📦 XIAOFU工具箱 v1.2.2 正式发布

🔄 更新内容：
1. 新增多图层压盖汇总
2. 新增地图生成界址点线
3. 交集汇总表工具优化
4. Bug修复与改进

📅 发布日期：2025年12月26日

💾 下载方式：
- 方式一：通过工具箱内置的"检查更新"功能自动更新
- 方式二：访问 Gitee 仓库手动下载安装包

⚠️ 重要提示：
- 该版本只支持 ArcGIS Pro 3.6+
- 在使用任何工具前，请务必备份原始数据

📧 反馈联系：
- QQ群：967758553
- 微信：fu76488
- 哔哩哔哩：XIAOFUGIS
```

---

## version.json 模板

```json
{
  "version": "X.X.X",
  "releaseDate": "YYYY-MM-DD",
  "minDesktopVersion": "3.6.0",
  "downloadUrl": "https://gitee.com/XFTools/xiaofutools/releases/download/vX.X.X/XIAOFUTools.esriAddinX",
  "changelog": [
    "更新项1",
    "更新项2"
  ],
  "notice": ""
}
```

---

## 发布检查清单

- [ ] 版本号已在所有相关文件中更新
- [ ] 更新内容描述准确清晰
- [ ] 代码已通过编译测试
- [ ] version.json 文件内容正确
- [ ] 插件文件已生成
- [ ] Gitee Release 已创建
- [ ] 下载链接已验证可用
- [ ] 公告已发布

---

**维护者**: XIAOFU
