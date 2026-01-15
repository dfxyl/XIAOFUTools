# 贡献指南

感谢你对 XIAOFUTools 项目的关注！本文档说明如何参与项目开发和贡献代码。

---

## 开发环境配置

### 必需软件

| 软件 | 版本 | 说明 |
|------|------|------|
| Visual Studio 2022 | 17.0+ | 主要开发 IDE |
| ArcGIS Pro | 3.6+ | 运行环境 |
| .NET SDK | 8.0 | 运行时框架 |
| ArcGIS Pro SDK | 3.6+ | 开发 SDK |

### 环境设置

1. 安装 ArcGIS Pro SDK for .NET（通过 Visual Studio 扩展）
2. 克隆仓库到本地
3. 使用 Visual Studio 打开 `XIAOFUTools.sln`
4. 还原 NuGet 包
5. 配置调试环境指向 ArcGIS Pro

### 调试配置

在 `Properties/launchSettings.json` 中配置：

```json
{
  "profiles": {
    "ArcGISPro": {
      "commandName": "Executable",
      "executablePath": "D:\\RUANJIAN\\ArcGIS\\Pro\\bin\\ArcGISPro.exe"
    }
  }
}
```

---

## 代码规范

### 分支策略

| 分支 | 用途 |
|------|------|
| `main` | 稳定发布版本 |
| `develop` | 开发主分支 |
| `feature/*` | 新功能开发 |
| `bugfix/*` | Bug 修复 |
| `release/*` | 发布准备 |

### 提交规范

使用语义化提交信息：

```
<type>(<scope>): <subject>

<body>

<footer>
```

类型说明：

| 类型 | 说明 |
|------|------|
| `feat` | 新功能 |
| `fix` | Bug 修复 |
| `docs` | 文档更新 |
| `style` | 代码格式 |
| `refactor` | 代码重构 |
| `perf` | 性能优化 |
| `test` | 测试相关 |
| `chore` | 构建/工具 |

示例：

```
feat(BatchLayerClip): 添加批量裁剪进度显示

- 添加进度条显示当前处理进度
- 支持取消操作
- 优化大数据量处理性能

Closes #123
```

---

## 开发流程

### 1. 创建分支

```bash
# 从 develop 创建功能分支
git checkout develop
git pull origin develop
git checkout -b feature/new-tool-name
```

### 2. 开发功能

遵循 [AGENTS.md](AGENTS.md) 中的开发规范进行开发。

### 3. 提交代码

```bash
git add .
git commit -m "feat(NewTool): 添加新工具功能"
```

### 4. 更新文档

在 `UNRELEASED.md` 中记录更改：

```markdown
## 新增功能

### 添加新工具 (2025-01-15)
- 功能描述
- 修改的文件列表
```

### 5. 推送分支

```bash
git push origin feature/new-tool-name
```

### 6. 创建 Pull Request

在 Gitee 上创建 PR，填写：
- 功能描述
- 测试说明
- 相关 Issue

---

## 版本发布

### 版本号规则

遵循语义化版本：`vMAJOR.MINOR.PATCH`

| 版本 | 说明 | 示例 |
|------|------|------|
| MAJOR | 不兼容的 API 修改 | v1.x.x → v2.0.0 |
| MINOR | 向下兼容的功能新增 | v1.2.x → v1.3.0 |
| PATCH | 向下兼容的问题修复 | v1.2.3 → v1.2.4 |

### 发布流程

1. 更新版本号
   - `Common/VersionInfo.cs`
   - `Config.daml`
   - `Tools/User/About/AboutDialog.xaml`

2. 更新 CHANGELOG.md
   - 将 UNRELEASED.md 内容移入
   - 添加版本号和日期

3. 构建发布版本
   ```bash
   dotnet build -c Release
   ```

4. 创建 Git 标签
   ```bash
   git tag v1.2.3
   git push origin v1.2.3
   ```

5. 在 Gitee 创建 Release

---

## 测试要求

### 功能测试

- [ ] 工具能正常打开
- [ ] 参数输入验证正确
- [ ] 处理逻辑正确
- [ ] 错误处理完善
- [ ] 取消操作有效

### 兼容性测试

- [ ] ArcGIS Pro 3.6+ 兼容
- [ ] 不同数据格式支持
- [ ] 大数据量处理

### 界面测试

- [ ] 样式显示正确
- [ ] 响应式布局
- [ ] 主题适配

---

## 问题反馈

### Bug 报告

提交 Issue 时请包含：

1. 问题描述
2. 复现步骤
3. 期望行为
4. 实际行为
5. 环境信息（ArcGIS Pro 版本、操作系统）
6. 错误日志（如有）

### 功能建议

提交功能建议时请说明：

1. 功能描述
2. 使用场景
3. 预期效果

---

## 联系方式

- **QQ群**: 967758553
- **微信**: fu76488
- **哔哩哔哩**: XIAOFUGIS
