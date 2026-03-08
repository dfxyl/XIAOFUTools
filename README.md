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

当前 Ribbon 按 `通用 / 编辑 / 数据 / 制图 / 系统` 5 组组织。

### 通用
- **查看面积** - 快速查看当前要素面积
- **添加预设图层** - 以内联图库方式展开常用底图与预设图层
- **历史影像** - 浏览 ArcGIS Wayback 历史影像
- **数据加载** - 包含 `批量添加数据`、`下载在线影像`、`启动Overture`

### 编辑
- **面积分割** - 地图交互式按面积分割要素
- **属性编辑** - 包含 `要素顺序编号`、`地块中文编号`、`字段复制工具`、`属性传递[字段]`
- **几何编辑** - 包含 `按字段批量裁剪要素`、`根据范围批量裁剪要素图层`、`旋转图形[线/面]`、`批量定义投影`、`批量修复几何`
- **界址编辑** - 包含 `生成四至坐标点`、`查看起始点`、`修改起始点`、`界址点线生成`、`地图生成界址点线`、`提取协议线`
- **界址成果** - 包含 `布局生成坐标表[要素图层版]`

### 数据
- **计算面积** - 当前数据组的顶级入口
- **分析检查** - 包含 `提取面扣岛`、`交集汇总表`、`多图层压盖汇总`、`数据透视`、`节点距离检查工具`、`图形重叠检查工具`、`缝隙检查工具`
- **格式转换** - 包含 `要素类转TXT`、`TXT转SHP`、`特殊坐标转换`、`要素图层分组导出KML/KMZ`、`面转DWG[带填充]`、`面转DXF[带填充]`
- **数据整备** - 包含 `批量合并SHP`、`MDB批量转GDB`、`镜像数据库`、`属性表建库`、`属性表建SHP`、`SHP输字段表`、`输出数据库属性结构表`

### 制图
- **布局图幅** - 包含 `驱动制图`、`导出布局`、`布局元素查找替换`、`生成小比例尺图幅`、`生成大比例尺图幅`
- **文档处理** - 包含 `PDF批量转图片`、`图片批量转PDF`、`Excel批量转PDF`、`Word批量转PDF`、`文档批量替换`

### 系统
- **AI助手** - 智能 GIS 操作建议
- **定制** - 包含 `开发区整合优化核查(定制)`
- **配置** - 包含 `设置`、`授权`、`关于`

### 图层右键工具
- **粘贴符号** - 图层右键菜单快速应用符号
- **导出CAD** - 图层右键导出 CAD
- **导出Excel** - 图层右键导出 Excel
- **匹配符号系列** - 包含 `匹配符号`、`DLMC匹配符号`、`DLBM匹配符号`、`FXDLMC匹配符号`、`FXDLBM匹配符号`、`BZKJDLMC匹配符号`、`BZKJDLBM匹配符号`、`YDYHFLDM匹配国空符号`、`YDYHFLMC匹配国空符号`

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
git clone https://github.com/xiaofuX1/XIAOFUTools.git

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
| [Docs/USER_GUIDE.md](Docs/USER_GUIDE.md) | 用户使用手册 |
| [Docs/FAQ.md](Docs/FAQ.md) | 常见问题解答 |
| [AGENTS.md](AGENTS.md) | 开发规范与架构指南 |
| [RELEASE.md](RELEASE.md) | 版本发布指南（GitHub） |
| [CHANGELOG.md](CHANGELOG.md) | 版本更新日志 |

## 项目结构

```
XIAOFUTools/
├── Common/              # 公共组件
├── Data/                # 数据文件（模板、图层）
├── Docs/                # 项目文档
├── Images/              # 图标资源
├── Styles/              # WPF 样式
├── Tools/               # 工具模块
│   ├── Analysis/        # 分析统计与右键扩展
│   ├── Common/          # 通用与地图加载工具
│   ├── Convert/         # 格式转换与导出工具
│   ├── DataProcessing/  # 数据整备、建库与图幅工具
│   ├── Edit/            # 编辑、界址与质检工具
│   ├── LayoutTools/     # 布局制图辅助工具
│   └── User/            # 用户与系统工具
├── Config.daml          # ArcGIS Pro 插件配置
├── Module1.cs           # 主模块入口
└── XIAOFUTools.csproj   # 项目文件
```

## 联系方式

- **QQ群**: 967758553
- **微信**: fu76488
- **哔哩哔哩**: XIAOFUGIS

## 许可证

本项目为私有项目，保留所有权利。

---

<p align="center">
  <sub>Made with ❤️ by XIAOFU</sub>
</p>
