// AI 助手前端渲染与格式化
// 由 tools/maintenance/split-ai-script.cjs 从原单文件按职责拆分。

function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

function updateSensitiveModeBanner(isEnabled) {
        const statusEl = document.getElementById('sensitiveModeStatus');
        if (!statusEl) {
            return;
        }

        const enabled = isEnabled !== false;
        statusEl.textContent = enabled ? '开启' : '关闭';
        statusEl.classList.toggle('on', enabled);
        statusEl.classList.toggle('off', !enabled);
    }

function updateContentIncremental(fullText) {
        // 查找最后一个代码块
        const lastCodeBlockMatch = fullText.match(/```([\w]*)?\n([\s\S]*)$/);
        
        if (lastCodeBlockMatch) {
            const language = lastCodeBlockMatch[1] || 'text';
            const code = lastCodeBlockMatch[2];
            
            // 查找或创建代码块容器
            let codeContainer = currentAiMsgContent.content.querySelector('.code-block-container:last-child');
            
            if (!codeContainer) {
                // 第一次检测到代码块,创建容器
                const codeId = 'code-' + Math.random().toString(36).substr(2, 9);
                const langDisplay = language.charAt(0).toUpperCase() + language.slice(1);
                const containerHtml = `<div class="code-block-container expanded" data-code-id="${codeId}"><div class="code-block-header" onclick="toggleCodeBlock('${codeId}')"><span class="code-block-lang">📝 ${langDisplay} <span class="loading-spinner"></span></span><div class="code-block-actions"><button class="code-block-copy" onclick="copyCode(event, '${codeId}')" title="复制代码">📋</button><span class="code-block-toggle">▼</span></div></div><div class="code-block-content"><pre><code></code></pre></div></div>`;
                currentAiMsgContent.content.insertAdjacentHTML('beforeend', containerHtml);
                codeContainer = currentAiMsgContent.content.querySelector('.code-block-container:last-child');
            }
            
            // 更新代码内容
            const codeElement = codeContainer.querySelector('code');
            if (codeElement) {
                // 代码高亮
                let highlightedCode = escapeHtml(code);
                if (typeof hljs !== 'undefined') {
                    try {
                        if (language && hljs.getLanguage(language)) {
                            highlightedCode = hljs.highlight(code, { language: language }).value;
                        } else {
                            highlightedCode = hljs.highlightAuto(code).value;
                        }
                    } catch (err) {
                        highlightedCode = escapeHtml(code);
                    }
                }
                codeElement.innerHTML = highlightedCode;
                
                // 滚动到底部
                const content = codeContainer.querySelector('.code-block-content');
                if (content) {
                    content.scrollTop = content.scrollHeight;
                }
            }
        }
    }

function formatContent(text) {
        if (!text) return '';
        
        // 0. 移除模型特殊标记
        text = removeModelTokens(text);
        
        // 1. 移除开头和结尾的空行
        text = text.trim();
        
        // 2. 检测是否包含表格（查找表格分隔符 |---|）
        const hasTable = /\|[-:]+\|/.test(text);
        
        // 如果包含表格，使用marked库解析
        if (hasTable && typeof marked !== 'undefined') {
            try {
                return marked.parse(text);
            } catch (e) {
                console.warn('Marked解析失败，使用默认格式化:', e);
            }
        }
        
        // 3. 移除空行(包括只有空格的行)
        text = text.split('\n').filter(line => line.trim() !== '').join('\n');
        
        // 3. 检测并处理代码块(支持未完成的代码块)
        const codeBlockRegex = /```([\w]*)?\n([\s\S]*?)(```|$)/g;
        let lastIndex = 0;
        let result = '';
        let match;
        
        while ((match = codeBlockRegex.exec(text)) !== null) {
            // 处理代码块之前的文本
            let beforeText = text.substring(lastIndex, match.index);
            result += formatPlainTextSimple(beforeText);
            
            // 处理代码块
            const language = match[1] || 'text';
            const code = match[2];
            const isComplete = match[3] === '```'; // 检查是否有结束标记
            const codeId = 'code-' + Math.random().toString(36).substr(2, 9);
            const langDisplay = language.charAt(0).toUpperCase() + language.slice(1);
            
            // 代码高亮
            let highlightedCode = escapeHtml(code);
            if (typeof hljs !== 'undefined') {
                try {
                    if (language && hljs.getLanguage(language)) {
                        highlightedCode = hljs.highlight(code, { language: language }).value;
                    } else {
                        highlightedCode = hljs.highlightAuto(code).value;
                    }
                } catch (err) {
                    highlightedCode = escapeHtml(code);
                }
            }
            
            // Python代码块添加运行按钮
            const isPython = language.toLowerCase() === 'python' || language.toLowerCase() === 'py';
            const runButton = isPython ? `<button class="code-block-run" onclick="runPythonCode(event, '${codeId}')" title="运行代码">▶</button>` : '';
            const resultArea = isPython ? `<div class="code-result-area" id="result-${codeId}" style="display:none;"></div>` : '';
            
            // 代码块默认关闭(不显示加载动画,避免闪烁)
            result += `<div class="code-block-container" data-code-id="${codeId}" data-lang="${language}"><div class="code-block-header" onclick="toggleCodeBlock('${codeId}')"><span class="code-block-lang">📝 ${langDisplay}</span><div class="code-block-actions">${runButton}<button class="code-block-copy" onclick="copyCode(event, '${codeId}')" title="复制代码">📋</button><span class="code-block-toggle">▼</span></div></div><div class="code-block-content"><pre><code>${highlightedCode}</code></pre></div>${resultArea}</div>`;
            
            lastIndex = codeBlockRegex.lastIndex;
        }
        
        // 处理最后一段文本
        let remainingText = text.substring(lastIndex);
        result += formatPlainTextSimple(remainingText);
        
        return result;
    }

function formatPlainTextSimple(text) {
        if (!text) return '';
        
        text = text.trim();
        if (!text) return '';
        
        // 转义HTML
        let formatted = escapeHtml(text);
        
        // 处理Markdown格式(按行处理)
        let lines = formatted.split('\n');
        let html = '';
        
        for (let i = 0; i < lines.length; i++) {
            let line = lines[i].trim();
            if (!line) continue;
            
            // 标题
            if (line.match(/^#{1,6}\s+/)) {
                const level = line.match(/^(#{1,6})/)[1].length;
                let content = line.replace(/^#{1,6}\s+/, '');
                content = processInlineFormats(content);
                html += `<h${level}>${content}</h${level}>`;
            }
            // 无序列表
            else if (line.match(/^[-*]\s+/)) {
                let content = line.replace(/^[-*]\s+/, '');
                content = processInlineFormats(content);
                html += `• ${content}<br>`;
            }
            // 有序列表
            else if (line.match(/^\d+\.\s+/)) {
                let content = line.replace(/^\d+\.\s+/, '');
                content = processInlineFormats(content);
                html += `▸ ${content}<br>`;
            }
            // 引用
            else if (line.match(/^&gt;\s+/)) {
                let content = line.replace(/^&gt;\s+/, '');
                content = processInlineFormats(content);
                html += `<span style="color:#64748b;border-left:3px solid #cbd5e1;padding-left:8px;display:inline-block;">${content}</span><br>`;
            }
            // 普通文本
            else {
                line = processInlineFormats(line);
                html += line + '<br>';
            }
        }
        
        return html;
    }

function processInlineFormats(text) {
        // 先处理行内代码(避免代码内的*被处理)
        text = text.replace(/`([^`]+)`/g, '<code>$1</code>');
        // 再处理粗体(必须在斜体之前)
        text = text.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
        // 最后处理斜体
        text = text.replace(/\*([^*]+)\*/g, '<em>$1</em>');
        return text;
    }

function scrollToBottom() {
        // 实时检查是否在底部，如果用户已向上滚动则不自动滚动
        if (isScrolledToBottom()) {
            chatList.scrollTop = chatList.scrollHeight;
        }
    }

function isScrolledToBottom() {
        const threshold = 100; // 允许100px的误差，避免小幅度滚动就停止自动滚动
        return chatList.scrollHeight - chatList.scrollTop - chatList.clientHeight < threshold;
    }

function updateScrollButton() {
        if (isScrolledToBottom()) {
            scrollToBottomBtn.style.display = 'none';
        } else {
            scrollToBottomBtn.style.display = 'flex';
        }
    }

function forceScrollToBottom() {
        chatList.scrollTop = chatList.scrollHeight;
        scrollToBottomBtn.style.display = 'none';
    }

function updateHistoryActiveState() {
        const items = historyList.querySelectorAll('.history-item');
        items.forEach(item => {
            if (item.dataset.sessionId === currentSessionId) {
                item.classList.add('active');
            } else {
                item.classList.remove('active');
            }
        });
    }

function formatTokenCount(tokenCount) {
        const value = Number(tokenCount) || 0;
        if (value <= 0) return '未设置';
        if (value >= 1000000) return `${(value / 1000000).toFixed(value % 1000000 === 0 ? 0 : 1)}M`;
        if (value >= 1000) return `${(value / 1000).toFixed(value % 1000 === 0 ? 0 : 1)}K`;
        return `${value}`;
    }

function formatTime(timestamp) {
        const date = new Date(timestamp);
        const now = new Date();
        const diff = now - date;
        
        // 小于1分钟
        if (diff < 60000) {
            return '刚刚';
        }
        // 小于1小时
        if (diff < 3600000) {
            return Math.floor(diff / 60000) + '分钟前';
        }
        // 小于1天
        if (diff < 86400000) {
            return Math.floor(diff / 3600000) + '小时前';
        }
        // 小于7天
        if (diff < 604800000) {
            return Math.floor(diff / 86400000) + '天前';
        }
        // 显示日期
        return date.toLocaleDateString('zh-CN', { month: '2-digit', day: '2-digit' });
    }

function updateImagePreview() {
        if (selectedImages.length === 0) {
            imagePreviewContainer.style.display = 'none';
            imagePreviewContainer.innerHTML = '';
            return;
        }
        
        imagePreviewContainer.style.display = 'flex';
        imagePreviewContainer.innerHTML = '';
        
        selectedImages.forEach((img, index) => {
            const previewItem = document.createElement('div');
            previewItem.className = 'image-preview-item';
            previewItem.innerHTML = `
                <img src="${img}" />
                <button class="image-preview-remove" onclick="removeImage(${index})">×</button>
            `;
            imagePreviewContainer.appendChild(previewItem);
        });
    }
