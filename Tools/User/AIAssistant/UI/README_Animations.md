# 代码块和思考模块动画特效说明

## ✨ 新增功能

### 1. **代码块自动展开/收起**

#### 行为
- ✅ **输出时自动展开** - 代码块生成时默认展开状态
- ✅ **输出完成自动收起** - streamEnd时自动折叠所有代码块
- ✅ **手动切换** - 点击代码块头部可手动展开/收起

#### 实现
```javascript
// 代码块默认展开
const expandedClass = 'expanded';
result += `<div class="code-block-container ${expandedClass}" ...>`;

// streamEnd时自动收起
const codeBlocks = currentAiMsgContent.content.querySelectorAll('.code-block-container.expanded');
codeBlocks.forEach(block => {
    block.classList.remove('expanded');
});
```

### 2. **加载动画特效** 🔄

#### 转圈圈动画
- **思考模块** - "正在思考..."时显示转圈圈
- **代码块** - 代码输出中显示转圈圈

#### CSS实现
```css
.loading-spinner {
    display: inline-block;
    width: 12px;
    height: 12px;
    border: 2px solid #93c5fd;
    border-top-color: transparent;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
}

@keyframes spin {
    0% { transform: rotate(0deg); }
    100% { transform: rotate(360deg); }
}
```

#### 应用位置
1. **思考模块** - `.thinking-loading::before`
2. **代码块** - `.loading-spinner` (未完成时显示)

### 3. **实时状态显示**

#### 代码块状态
```javascript
const langDisplay = language + (isComplete ? '' : ' (输出中...)');
const loadingIcon = isComplete ? '' : '<span class="loading-spinner"></span>';
```

**效果:**
- 输出中: `📝 Javascript (输出中...) 🔄`
- 输出完成: `📝 Javascript`

## 🎯 用户体验优化

### 展开/收起流程

#### 思考模块
1. **开始思考** → 展开 + "思考过程"
2. **收到第一个回复** → 自动收起 + "思考过程（点击展开）"
3. **手动点击** → 切换展开/收起

#### 代码块
1. **开始输出** → 展开 + 转圈圈动画
2. **输出完成** → 自动收起 + 移除动画
3. **手动点击** → 切换展开/收起

### 视觉反馈

#### 加载状态
- ✅ 转圈圈动画 - 0.8秒一圈
- ✅ 蓝色边框 - 与模块主题色一致
- ✅ 文字提示 - "(输出中...)"

#### 交互状态
- ✅ 鼠标悬停 - 背景色变化
- ✅ 展开/收起 - 箭头旋转
- ✅ 平滑过渡 - transition动画

## 📋 完整流程示例

### AI回复流程

```
1. streamStart
   ├─ 创建AI消息
   └─ 创建思考模块(折叠)

2. streamThinking
   ├─ 展开思考模块
   ├─ 显示 "思考过程"
   └─ 显示转圈圈动画 🔄

3. streamChunk (第一个)
   ├─ 自动收起思考模块
   └─ 更新标题 "思考过程（点击展开）"

4. streamChunk (检测到代码块)
   ├─ 创建代码块容器(展开)
   ├─ 显示 "Javascript (输出中...)"
   └─ 显示转圈圈动画 🔄

5. streamChunk (代码继续输出)
   ├─ 实时更新代码内容
   └─ 保持展开状态 + 动画

6. streamEnd
   ├─ 最后一次解析Markdown
   ├─ 自动收起所有代码块
   ├─ 移除 "(输出中...)" 标记
   └─ 移除转圈圈动画
```

## 🎨 样式细节

### 思考模块
- **背景色**: `#fefce8` (浅黄色)
- **边框色**: `#fde047` (黄色)
- **动画色**: `#fde047` (黄色边框)

### 代码块
- **背景色**: `#eff6ff` (浅蓝色)
- **边框色**: `#93c5fd` (蓝色)
- **动画色**: `#93c5fd` (蓝色边框)

### 动画参数
- **旋转速度**: 0.8秒/圈
- **动画类型**: linear (匀速)
- **尺寸**: 12px × 12px
- **边框宽度**: 2px

## 🔧 技术实现

### 关键代码位置

#### JavaScript
- `formatContent()` - 代码块生成逻辑
- `streamEnd` - 自动收起代码块
- `streamThinking` - 思考模块展开
- `streamChunk` - 思考模块自动收起

#### CSS
- `.loading-spinner` - 转圈圈动画
- `.thinking-loading::before` - 思考加载动画
- `@keyframes spin` - 旋转动画定义
- `.code-block-container.expanded` - 展开状态

## 📝 注意事项

1. **动画性能** - 使用CSS transform,GPU加速
2. **状态同步** - 确保展开/收起状态一致
3. **用户体验** - 自动操作不干扰手动操作
4. **视觉一致** - 思考和代码模块样式统一

## 🚀 未来优化方向

- [ ] 可配置动画速度
- [ ] 更多动画效果选择
- [ ] 自定义主题色
- [ ] 动画开关设置
