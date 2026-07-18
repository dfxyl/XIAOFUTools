# XIAOFUTools

<p align="center">
  <img src="Assets/Images/Toolbox_32.png" alt="XIAOFUTools Logo" width="64" height="64">
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
  <a href="#许可证">许可证</a> •
  <a href="#联系方式">联系方式</a>
</p>

---

## 概述

XIAOFUTools 是一款基于 ArcGIS Pro SDK 开发的专业 GIS 扩展工具箱，采用 .NET 10.0 框架和 WPF/MVVM 架构，为 GIS 从业者提供高效、便捷的数据处理和分析解决方案。

## 功能特性

当前 Ribbon 按 通用 / 编辑 / 数据 / 制图 / 系统 5 组组织；另提供可选的独立 GIS 工具口袋 标签页。

### GIS 工具口袋
- **工具箱合并** - 原 GIS Toolbox 已整合进 XIAOFU工具箱，无需再安装第二个插件。
- **多来源收纳** - 支持添加 .atbx、.tbx、.pyt 工具箱，以及 ArcGIS Pro 内置命令、地图工具和自定义工具组。
- **快速调用** - 支持工具搜索、工具箱层级浏览、显示/隐藏、拖拽排序和最多 32 个动态入口。
- **配置迁移** - 支持完整工具包导入导出，并兼容读取原 GIS Toolbox 配置。

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
- **数据整备** - 包含 `批量合并SHP`、`MDB批量格式转换`、`镜像数据库`、`属性表建库`、`属性表建SHP`、`SHP输字段表`、`输出数据库属性结构表`

### 制图
- **布局图幅** - 包含 `驱动制图`、`导出布局`、`布局元素查找替换`、`生成小比例尺图幅`、`生成大比例尺图幅`
- **文档处理** - 包含 `PDF批量转图片`、`图片批量转PDF`、`Excel批量转PDF`、`Word批量转PDF`、`文档批量替换`

### 系统
- **AI助手** - 智能 GIS 操作建议
- **定制** - 包含 `开发区整合优化核查(定制)`
- **配置** - 包含 `设置`、`关于`

### 图层右键工具
- **粘贴符号** - 图层右键菜单快速应用符号
- **导出CAD** - 图层右键导出 CAD
- **导出Excel** - 图层右键导出 Excel
- **匹配符号系列** - 包含 `匹配符号`、`DLMC匹配符号`、`DLBM匹配符号`、`FXDLMC匹配符号`、`FXDLBM匹配符号`、`BZKJDLMC匹配符号`、`BZKJDLBM匹配符号`、`YDYHFLDM匹配国空符号`、`YDYHFLMC匹配国空符号`

## 系统要求

| 组件 | 版本要求 |
|------|----------|
| ArcGIS Pro | 3.7 |
| .NET | 10.0 |
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

# 使用 Visual Studio 2022 打开解决方案，选择 Release / x64
# 非标准安装目录通过 ArcGISProInstallDir 指定
dotnet build XIAOFUTools.sln -c Release -p:Platform=x64 -p:ArcGISProInstallDir="D:\ArcGIS\Pro"
```

## 快速开始

1. 启动 ArcGIS Pro
2. 在功能区找到 **XIAOFU工具箱** 或 **GIS 工具口袋** 选项卡
3. 首次使用 GIS 工具口袋时，点击“管理”添加工具箱或常用命令
4. 如需隐藏该标签页，可在 **XIAOFU工具箱 > 系统 > 设置** 中关闭“显示 GIS 工具口袋标签页”

## 文档

| 文档 | 说明 |
|------|------|
| [docs/user-guide.md](docs/user-guide.md) | 用户使用手册 |
| [docs/faq.md](docs/faq.md) | 常见问题解答 |
| [docs/architecture.md](docs/architecture.md) | 架构与依赖边界 |
| [docs/development.md](docs/development.md) | 开发规范与新增功能流程 |
| [docs/verification.md](docs/verification.md) | 架构治理与 ArcGIS Pro 宿主验收 |
| [docs/releasing.md](docs/releasing.md) | 版本发布指南 |
| [CHANGELOG.md](CHANGELOG.md) | 版本更新日志 |

## 项目结构

```
XIAOFUTools/
├── App/                 # Add-in 生命周期与组合入口
├── Shared/              # ArcGIS、MVVM、诊断、IO 与公共界面基础设施
├── Features/            # 按业务域组织的功能模块
├── Assets/              # 图标、模板、图层和符号库
├── docs/                # 架构、开发、用户和发布文档
├── tools/               # 图标与维护脚本
├── tests/               # 自动化测试工程
├── Config.daml          # ArcGIS Pro 插件配置
└── XIAOFUTools.csproj   # 项目文件
```

## 联系方式

- **QQ群**: 967758553
- **微信**: fu76488
- **哔哩哔哩**: XIAOFUGIS

## 许可证

本项目基于 [MIT License](LICENSE) 开源发布。ArcGIS Pro、ArcGIS Pro SDK 以及第三方服务的使用许可由其各自提供方约束。

---

<p align="center">
  <sub>Made with ❤️ by XIAOFU</sub>
</p>
