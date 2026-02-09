# AI助手功能模块

## 功能概述
完整的GIS AI助手功能模块，提供智能对话、GIS操作辅助和上下文对话历史管理。

## 架构说明

### 1. 数据库管理层 (Database/)
- **DatabaseManager.cs**: SQLite数据库管理器,管理AI服务配置和对话历史
- **Models.cs**: 数据模型定义(AIServiceConfig, ConversationMessage, Session等)

### 2. AI服务层 (Services/)
- **IAIService.cs**: AI服务接口定义
- **OpenAICompatibleService.cs**: OpenAI兼容API实现(支持DeepSeek、SiliconFlow等)

### 3. GIS Agent层 (Agent/)
- **IGISTool.cs**: GIS工具接口定义
- **GISAgentCore.cs**: GIS Agent核心,协调AI服务和工具调用
- **Tools/WebSearchTool.cs**: 联网查资料工具(内置)

### 4. 应用编排层 (Application/)
- **AIAssistantApplicationService.cs**: 会话、消息、模型、Python执行统一编排

### 5. UI层 (UI/)
- **chat.html**: 现代化聊天界面(响应式设计,流式输出)
- **AIAssistantDockPaneView.xaml**: WPF视图(嵌入WebView2)
- **AIAssistantDockPaneView.xaml.cs**: 视图代码后台

### 6. 视图模型和按钮
- **AIAssistantDockPaneViewModel.cs**: DockPane ViewModel
- **AIAssistantButton.cs**: 工具箱按钮

## 内置功能特性

### 已实现
- ✅ SQLite数据库管理(配置、历史、会话)
- ✅ 双AI服务支持(DeepSeek、SiliconFlow GLM-4.5V)
- ✅ 流式响应输出
- ✅ 对话上下文管理
- ✅ OpenAI原生Tools协议(tool_choice / tool_calls)
- ✅ 联网工具自动调用（AI自主判断，无需手动开启）
- ✅ 现代化UI界面(渐变色、动画效果)
- ✅ 工具模块UI（联网查资料）
- ✅ WebView2集成
- ✅ 多轮对话支持
- ✅ 会话管理(新建、切换、清空)

### 待扩展
- ⚪ GIS工具注册和调用
- ⚪ 工具调用检测和执行
- ⚪ ArcGIS Pro API集成
- ⚪ 地图操作工具
- ⚪ 数据分析工具
- ⚪ 坐标转换工具

## 配置说明

### AI服务配置
系统内置两个AI服务,存储在SQLite数据库中:

1. **DeepSeek**
   - 端点: https://api.deepseek.com/v1
   - 模型: deepseek-chat
   - 默认服务: 是

2. **SiliconFlow GLM-4.5V**
   - 端点: https://api.siliconflow.cn/v1/
   - 模型: zai-org/GLM-4.5V
   - 默认服务: 否

### 数据存储位置
- 数据库路径: `%AppData%\XIAOFUTools\AIAssistant\aiassistant.db`
- 包含: AI服务配置、对话历史、会话记录、工具调用日志

## 使用方法

1. 点击"用户"功能区中的"AI助手"按钮
2. 在打开的DockPane中与AI对话
3. 支持的操作:
   - 输入问题并发送
   - 清空当前对话
   - 创建新会话
   - 查看流式响应

## 扩展开发

### 添加新的GIS工具
1. 实现`IGISTool`接口
2. 在`GISAgentCore`中注册工具:
```csharp
_agentCore.RegisterTool(new YourGISTool());
```

### 添加新的AI服务
通过DatabaseManager添加:
```csharp
DatabaseManager.Instance.InsertService(new AIServiceConfig
{
    Name = "服务名称",
    ApiEndpoint = "API端点",
    ModelName = "模型名称",
    ApiKey = "API密钥",
    IsDefault = false
});
```

## 技术栈
- .NET 8.0
- ArcGIS Pro SDK 3.5.2
- WebView2
- Microsoft.Data.Sqlite
- Newtonsoft.Json
- OpenAI兼容API

## 图标文件说明
需要添加以下图标文件到 `Images/` 目录:
- **AIAssistant_16.png** (16x16像素)
- **AIAssistant_32.png** (32x32像素)

建议使用🤖机器人图标或AI相关图标。

## 注意事项
1. 首次运行会自动创建数据库和表结构
2. API密钥已内置,可通过数据库更新
3. WebView2需要Edge浏览器支持
4. 流式响应依赖网络连接质量
5. 对话历史默认保留最近50条消息

## 许可和版权
遵循XIAOFUTools工具箱的许可协议。
