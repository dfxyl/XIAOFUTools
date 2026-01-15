# 思考模块数据库存储修复说明

## 🐛 问题描述

历史会话加载时,思考模块不显示。

## 🔍 根本原因

数据库没有保存和加载AI的thinking(思考过程)字段。

## ✅ 修复内容

### 1. **数据库模型** - `Models.cs`

添加`Thinking`字段到`ConversationMessage`模型:

```csharp
public class ConversationMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
    public string Thinking { get; set; }  // AI的思考过程(仅Agent模式)
    public DateTime Timestamp { get; set; }
    public int TokenCount { get; set; }
}
```

### 2. **数据库表结构** - `DatabaseManager.cs`

#### 添加thinking列
```csharp
CREATE TABLE IF NOT EXISTS conversation_history (
    id INTEGER PRIMARY KEY,
    session_id VARCHAR NOT NULL,
    role VARCHAR NOT NULL,
    content TEXT NOT NULL,
    thinking TEXT,              // 新增
    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    token_count INTEGER DEFAULT 0
)
```

#### 兼容性处理
```csharp
// 如果表已存在但没有thinking列,添加它
try
{
    using (var alterCmd = _connection.CreateCommand())
    {
        alterCmd.CommandText = "ALTER TABLE conversation_history ADD COLUMN IF NOT EXISTS thinking TEXT";
        alterCmd.ExecuteNonQuery();
    }
}
catch { /* 列已存在或其他错误,忽略 */ }
```

### 3. **保存消息** - `DatabaseManager.SaveMessage()`

#### 修改方法签名
```csharp
// 之前
public void SaveMessage(string sessionId, string role, string content, int tokenCount = 0)

// 现在
public void SaveMessage(string sessionId, string role, string content, int tokenCount = 0, string thinking = null)
```

#### 修改SQL
```csharp
cmd.CommandText = @"
    INSERT INTO conversation_history (session_id, role, content, thinking, token_count)
    VALUES (?, ?, ?, ?, ?)";

cmd.Parameters.Add(new DuckDBParameter(sessionId));
cmd.Parameters.Add(new DuckDBParameter(role));
cmd.Parameters.Add(new DuckDBParameter(content));
cmd.Parameters.Add(new DuckDBParameter(thinking ?? string.Empty));  // 新增
cmd.Parameters.Add(new DuckDBParameter(tokenCount));
```

### 4. **加载消息** - `DatabaseManager.GetConversationHistory()`

#### 修改SQL查询
```csharp
// 之前
SELECT role, content, timestamp, token_count 
FROM conversation_history

// 现在
SELECT role, content, thinking, timestamp, token_count 
FROM conversation_history
```

#### 修改读取逻辑
```csharp
messages.Add(new ConversationMessage
{
    Role = reader.GetString(0),
    Content = reader.GetString(1),
    Thinking = reader.IsDBNull(2) ? null : reader.GetString(2),  // 新增
    Timestamp = reader.GetDateTime(3),
    TokenCount = reader.GetInt32(4)
});
```

### 5. **Agent核心** - `GISAgentCore.cs`

#### 添加字段存储thinking
```csharp
private string _currentThinking = ""; // 当前的思考内容
```

#### 累积thinking内容
```csharp
// 重置思考内容
_currentThinking = "";

// 发送到AI服务
var response = await _aiService.SendMessageStreamAsync(
    messages,
    onChunkReceived,
    cancellationToken,
    (reasoning) => {
        _currentThinking += reasoning;  // 累积
        onReasoningReceived?.Invoke(reasoning);
    });
```

#### 保存时传递thinking
```csharp
// 保存AI响应(包括思考内容)
_dbManager.SaveMessage(_currentSessionId, "assistant", response, 0, _currentThinking);
```

### 6. **前端加载** - `script.js`

已在之前修复,加载历史时检查thinking字段:

```javascript
case 'sessionLoaded':
    data.messages.forEach(msg => {
        if (msg.role === 'user') {
            appendMessage('user', msg.content);
        } else {
            // AI消息,如果有thinking字段则显示思考模块
            const hasThinking = msg.thinking && msg.thinking.trim() !== '';
            const msgContent = appendMessage('ai', msg.content, hasThinking);
            
            // 如果有思考内容,填充到思考模块
            if (hasThinking && msgContent && msgContent.thinking) {
                msgContent.thinkingContainer.style.display = 'block';
                msgContent.thinking.innerHTML = escapeHtml(msg.thinking).replace(/\n/g, '<br>');
                msgContent.thinkingTitle.textContent = '思考过程（点击展开）';
            }
        }
    });
    break;
```

## 📊 数据流程

### 保存流程
```
AI回复 → streamThinking → 累积到_currentThinking
                                ↓
                         streamEnd → SaveMessage(content, thinking)
                                ↓
                         数据库 conversation_history表
```

### 加载流程
```
点击历史会话 → GetConversationHistory()
                        ↓
                 读取thinking字段
                        ↓
                 sessionLoaded事件
                        ↓
                 前端显示思考模块
```

## 🔧 兼容性

### 旧数据库升级
- ✅ 自动添加thinking列(如果不存在)
- ✅ 旧记录thinking为NULL或空字符串
- ✅ 不影响现有功能

### 新旧版本
- ✅ 新版本可以读取旧数据(thinking为空)
- ✅ 旧版本忽略thinking列(不影响)

## 📝 测试清单

- [x] 新会话保存thinking
- [x] 历史会话加载thinking
- [x] thinking为空时不显示模块
- [x] thinking有内容时显示模块(折叠状态)
- [x] 点击展开/收起thinking
- [x] 数据库表自动升级
- [x] 旧数据兼容性

## 🎯 效果

### 之前
```
历史会话:
  用户: 问题
  AI: 回答
  [思考模块不显示] ❌
```

### 现在
```
历史会话:
  用户: 问题
  AI: 回答
  💭 思考过程（点击展开） ✅
```

## 🚀 使用方法

1. **重新生成项目** - 数据库会自动升级
2. **发送新消息** - thinking会自动保存
3. **查看历史** - thinking会正常显示

## ⚠️ 注意事项

1. **旧历史记录** - 没有thinking内容(正常)
2. **Chat模式** - 没有thinking(只有Agent模式有)
3. **数据库升级** - 自动进行,无需手动操作
