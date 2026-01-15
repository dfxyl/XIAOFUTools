# XIAOFUTools

<p align="center">
  <img src="Images/Toolbox_32.png" alt="XIAOFUTools Logo" width="64" height="64">
</p>

<p align="center">
  <strong>ArcGIS Pro 扩展工具箱</strong><br>
  提供丰富的 GIS 数据处理、分析和制图功能
</p>

<p align="center">
  <a href="#功能特性">功能特性</a> •
  <a href="#安装说明">安装说明</a> •
  <a href="#快速开始">快速开始</a> •
  <a href="#文档">文档</a> •
  <a href="#联系方式">联系方式</a>
</p>

---

## 概述

XIAOFUTools 是一款基于 ArcGIS Pro SDK 开发的专业 GIS 扩展工具箱，采用 .NET 8.0 框架和 WPF/MVVM 架构，为 GIS 从业者提供高效、便捷的数据处理和分析解决方案。

## 功能特性

### 🗺️ 通用工具
- **批量添加数据** - 扫描文件夹批量添加 GIS 数据到地图
- **要素顺序编号** - 按分组字段对要素进行顺序编号
- **地块中文编号** - 使用中文数字对要素编号
- **添加预设图层** - 快速添加常用底图图层
- **历史影像** - 浏览 ArcGIS Wayback 历史影像

### ✏️ 编辑/计算工具
- **界址点线生成** - 生成界址点、界址线和标注
- **修改起始点** - 调整面要素的起始点位置
- **布局生成坐标表** - 在布局中生成坐标表
- **面积分割** - 按指定面积分割面要素
- **计算面积** - 批量计算面要素面积

### 🔄 转换工具
- **要素类转 TXT** - 导出要素坐标为文本文件
- **TXT 转 SHP** - 从文本文件创建要素类
- **特殊坐标转换** - 支持多种坐标格式转换
- **面转 DWG/DXF** - 导出带填充的 CAD 文件
- **PDF/图片批量转换** - 多种格式互转

### 📊 数据分析/处理
- **交集汇总表** - 计算图层交集面积统计
- **多图层压盖汇总** - 多图层交集面积分析
- **图形检查** - 节点距离、重叠、缝隙检查
- **属性表建库** - 根据 Excel 模板创建数据库
- **镜像数据库** - 复制数据库结构

### 🛠️ 用户工具
- **AI 助手** - 智能 GIS 操作建议
- **设置** - 工具箱配置管理
- **授权管理** - 软件授权状态管理

## 系统要求

| 组件 | 版本要求 |
|------|----------|
| ArcGIS Pro | 3.6+ |
| .NET | 8.0 |
| Windows | 10/11 (64-bit) |

## 安装说明

### 方式一：直接安装
1. 下载最新版本的 `XIAOFUTools.esriAddinX` 文件
2. 双击安装文件，按提示完成安装
3. 重启 ArcGIS Pro

### 方式二：从源码构建
```bash
# 克隆仓库
git clone https://gitee.com/XFTools/xiaofutools.git

# 使用 Visual Studio 2022 打开解决方案
# 编译 Release 版本
dotnet build -c Release
```

## 快速开始

1. 启动 ArcGIS Pro
2. 在功能区找到 **XIAOFU工具箱** 选项卡
3. 选择需要的工具开始使用

## 文档

| 文档 | 说明 |
|------|------|
| [AGENTS.md](AGENTS.md) | 开发规范与架构指南 |
| [CONTRIBUTING.md](CONTRIBUTING.md) | 贡献指南 |
| [CHANGELOG.md](CHANGELOG.md) | 版本更新日志 |

## 项目结构

```
XIAOFUTools/
├── Common/              # 公共组件
├── Data/                # 数据文件（模板、图层）
├── Images/              # 图标资源
├── Styles/              # WPF 样式
├── Tools/               # 工具模块
│   ├── Analysis/        # 分析工具
│   ├── Common/          # 通用工具
│   ├── Convert/         # 转换工具
│   ├── DataProcessing/  # 数据处理工具
│   ├── Edit/            # 编辑工具
│   └── User/            # 用户工具
├── Config.daml          # ArcGIS Pro 插件配置
├── Module1.cs           # 主模块入口
└── XIAOFUTools.csproj   # 项目文件
```

## 联系方式

- **QQ群**: 967758553
- **微信**: fu76488
- **哔哩哔哩**: XIAOFUGIS

## 许可证

本项目为私有项目，未经授权不得用于商业用途。

---

<p align="center">
  <sub>Made with ❤️ by XIAOFU</sub>
</p>
