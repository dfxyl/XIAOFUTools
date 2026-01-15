# Marked.js 本地库集成说明

## 📦 已集成的库

### 1. **Marked.js** (v11.1.1)
- 路径: `lib/marked.min.js`
- 功能: 完整的Markdown解析器
- 官网: https://marked.js.org/

### 2. **Highlight.js** (v11.9.0)
- 路径: `lib/highlight.min.js`
- 功能: 代码语法高亮
- 官网: https://highlightjs.org/

### 3. **GitHub主题样式**
- 路径: `lib/github.min.css`
- 功能: 代码高亮的GitHub风格样式

## ✨ 支持的Markdown功能

### 标题
```markdown
# 一级标题
## 二级标题
### 三级标题
```

### 列表
```markdown
- 无序列表项1
- 无序列表项2

1. 有序列表项1
2. 有序列表项2
```

### 强调
```markdown
**粗体文本**
*斜体文本*
`行内代码`
```

### 代码块
````markdown
```javascript
function hello() {
    console.log("Hello World!");
}
```
````

### 引用
```markdown
> 这是一段引用文本
```

### 链接和图片
```markdown
[链接文本](https://example.com)
![图片描述](image.png)
```

### 表格
```markdown
| 列1 | 列2 | 列3 |
|-----|-----|-----|
| 数据1 | 数据2 | 数据3 |
```

## 🎨 特殊功能

### 代码块容器
- ✅ 自动折叠(默认折叠状态)
- ✅ 复制按钮(点击复制代码)
- ✅ 语言标识(显示代码语言)
- ✅ 语法高亮(支持100+编程语言)
- ✅ 蓝色主题(与思考模块样式统一)

### GFM支持
- ✅ GitHub Flavored Markdown
- ✅ 自动换行(breaks: true)
- ✅ 删除线 `~~删除文本~~`
- ✅ 任务列表 `- [ ] 未完成` `- [x] 已完成`

## 🔧 技术实现

### 自定义Renderer
```javascript
const renderer = new marked.Renderer();

renderer.code = function(code, language) {
    // 生成可折叠的代码块容器
    // 包含复制按钮和语法高亮
};
```

### 配置选项
```javascript
marked.setOptions({
    renderer: renderer,
    breaks: true,  // 支持GFM换行
    gfm: true      // GitHub Flavored Markdown
});
```

## 📝 优势

### vs 正则表达式实现
- ✅ **完整性**: 支持所有标准Markdown语法
- ✅ **可靠性**: 经过大量测试的成熟库
- ✅ **扩展性**: 易于添加新功能
- ✅ **维护性**: 代码更简洁清晰
- ✅ **兼容性**: 符合CommonMark标准

### vs CDN引用
- ✅ **速度**: 无网络延迟
- ✅ **稳定**: 不受追踪阻止影响
- ✅ **可靠**: 不依赖外部服务
- ✅ **离线**: 完全本地运行

## 🚀 使用方法

### 重新加载AI助手
关闭并重新打开AI助手窗口,新代码即可生效。

### 测试Markdown
向AI发送消息,AI的回复将自动渲染Markdown格式。

## 📌 注意事项

1. **库文件位置**: 必须保持在`UI/lib/`目录下
2. **相对路径**: HTML引用使用相对路径`lib/xxx.js`
3. **加载顺序**: marked和hljs必须在script.js之前加载
4. **缓存问题**: ArcGIS Pro的WebView可能缓存旧代码,需要重启

## 🔄 更新库版本

如需更新库版本,使用PowerShell下载新版本:

```powershell
# 更新marked.js
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/marked@最新版本/marked.min.js" `
    -OutFile "lib/marked.min.js"

# 更新highlight.js
Invoke-WebRequest -Uri "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/最新版本/highlight.min.js" `
    -OutFile "lib/highlight.min.js"
```

## 📄 相关文件

- `chat.html` - 引用本地库文件
- `script.js` - 配置marked和自定义renderer
- `style.css` - 代码块容器样式
- `lib/marked.min.js` - Markdown解析器
- `lib/highlight.min.js` - 代码高亮库
- `lib/github.min.css` - GitHub主题样式
