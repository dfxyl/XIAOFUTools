// 全局函数: 切换代码块展开/折叠
function toggleCodeBlock(codeId) {
    const container = document.querySelector(`[data-code-id="${codeId}"]`);
    if (container) {
        container.classList.toggle('expanded');
    }
}

// 全局函数: 关闭结果区域
function closeResultArea(codeId) {
    const resultArea = document.getElementById(`result-${codeId}`);
    if (resultArea) {
        resultArea.style.display = 'none';
    }
}

// 全局函数: 复制执行结果
function copyExecutionResult(codeId) {
    const resultArea = document.getElementById(`result-${codeId}`);
    if (!resultArea) return;
    
    // 获取存储的执行数据
    const execData = resultArea.dataset.execData;
    if (!execData) return;
    
    const data = JSON.parse(execData);
    let copyText = '';
    
    if (data.output && data.output.trim()) {
        copyText += `输出:\n${data.output}\n`;
    }
    if (data.error && data.error.trim()) {
        copyText += `${copyText ? '\n' : ''}错误:\n${data.error}`;
    }
    if (!copyText) {
        copyText = data.success ? '执行完成，无输出' : '执行失败';
    }
    
    navigator.clipboard.writeText(copyText).then(() => {
        // 显示复制成功提示
        const copyBtn = resultArea.querySelector('.result-copy');
        if (copyBtn) {
            const originalText = copyBtn.textContent;
            copyBtn.textContent = '✓';
            setTimeout(() => { copyBtn.textContent = originalText; }, 1500);
        }
    }).catch(err => {
        console.error('复制失败:', err);
    });
}

// 全局函数: @引用执行结果到输入框（插入引用标签）
function quoteExecutionResult(codeId) {
    const resultArea = document.getElementById(`result-${codeId}`);
    if (!resultArea) return;
    
    const execData = resultArea.dataset.execData;
    if (!execData) return;
    
    const data = JSON.parse(execData);
    const execId = data.executionId || codeId;
    
    // 存储引用数据到全局Map
    if (!window.pythonExecReferences) {
        window.pythonExecReferences = new Map();
    }
    window.pythonExecReferences.set(execId.toString(), data);
    
    // 在输入框后插入引用标签
    const previewContainer = document.getElementById('imagePreviewContainer');
    if (previewContainer) {
        previewContainer.style.display = 'flex';
        
        // 检查是否已存在相同引用
        const existingRef = previewContainer.querySelector(`[data-exec-ref="${execId}"]`);
        if (existingRef) {
            // 已存在，闪烁提示
            existingRef.classList.add('flash');
            setTimeout(() => existingRef.classList.remove('flash'), 500);
            return;
        }
        
        // 创建引用标签
        const refTag = document.createElement('div');
        refTag.className = 'exec-ref-tag';
        refTag.dataset.execRef = execId;
        
        const statusIcon = data.success ? '✅' : '❌';
        const codePreview = (data.code || '').substring(0, 20) + (data.code?.length > 20 ? '...' : '');
        
        refTag.innerHTML = `
            <span class="exec-ref-icon">${statusIcon}</span>
            <span class="exec-ref-label">@执行#${execId}</span>
            <span class="exec-ref-preview" title="${codePreview}">${codePreview}</span>
            <button class="exec-ref-remove" onclick="removeExecReference('${execId}')" title="移除引用">✕</button>
        `;
        previewContainer.appendChild(refTag);
    }
    
    // 显示引用成功提示
    const quoteBtn = resultArea.querySelector('.result-quote');
    if (quoteBtn) {
        const originalText = quoteBtn.textContent;
        quoteBtn.textContent = '✓';
        setTimeout(() => { quoteBtn.textContent = originalText; }, 1500);
    }
}

// 全局函数: 移除执行结果引用
function removeExecReference(execId) {
    const previewContainer = document.getElementById('imagePreviewContainer');
    if (previewContainer) {
        const refTag = previewContainer.querySelector(`[data-exec-ref="${execId}"]`);
        if (refTag) {
            refTag.remove();
        }
        // 如果没有其他内容，隐藏容器
        if (previewContainer.children.length === 0) {
            previewContainer.style.display = 'none';
        }
    }
    // 从全局Map移除
    if (window.pythonExecReferences) {
        window.pythonExecReferences.delete(execId.toString());
    }
}

// 全局函数: 切换执行结果展开/折叠
function toggleResultArea(codeId) {
    const resultArea = document.getElementById(`result-${codeId}`);
    if (resultArea) {
        resultArea.classList.toggle('collapsed');
    }
}

// 危险操作检测模式
const DANGEROUS_PATTERNS = [
    // 文件删除操作
    { pattern: /os\.remove\s*\(/i, desc: '删除文件 (os.remove)' },
    { pattern: /os\.rmdir\s*\(/i, desc: '删除目录 (os.rmdir)' },
    { pattern: /os\.unlink\s*\(/i, desc: '删除文件 (os.unlink)' },
    { pattern: /shutil\.rmtree\s*\(/i, desc: '递归删除目录 (shutil.rmtree)' },
    { pattern: /shutil\.move\s*\(/i, desc: '移动/重命名文件 (shutil.move)' },
    // ArcPy危险操作
    { pattern: /arcpy\.Delete_management\s*\(/i, desc: '删除要素/数据 (Delete_management)' },
    { pattern: /arcpy\.DeleteFeatures_management\s*\(/i, desc: '删除要素 (DeleteFeatures)' },
    { pattern: /arcpy\.DeleteRows_management\s*\(/i, desc: '删除行 (DeleteRows)' },
    { pattern: /arcpy\.Truncate\s*\(/i, desc: '截断表 (Truncate)' },
    { pattern: /arcpy\.TruncateTable_management\s*\(/i, desc: '截断表 (TruncateTable)' },
    { pattern: /\.deleteRow\s*\(/i, desc: '删除游标行 (deleteRow)' },
    // 系统命令
    { pattern: /os\.system\s*\(/i, desc: '执行系统命令 (os.system)' },
    { pattern: /subprocess\.(run|call|Popen)\s*\(/i, desc: '执行子进程 (subprocess)' },
    // 数据库危险操作
    { pattern: /\bDROP\s+(TABLE|DATABASE|INDEX)/i, desc: 'SQL删除操作 (DROP)' },
    { pattern: /\bDELETE\s+FROM\b/i, desc: 'SQL删除数据 (DELETE)' },
    { pattern: /\bTRUNCATE\s+TABLE\b/i, desc: 'SQL截断表 (TRUNCATE)' },
    // 文件覆盖写入
    { pattern: /open\s*\([^)]*['"][wa]['"][^)]*\)/i, desc: '写入/覆盖文件 (open write)' },
];

// 检测代码中的危险操作
function detectDangerousOperations(code) {
    const found = [];
    for (const item of DANGEROUS_PATTERNS) {
        if (item.pattern.test(code)) {
            found.push(item.desc);
        }
    }
    return found;
}

// 全局函数: 运行Python代码
function runPythonCode(event, codeId) {
    event.stopPropagation(); // 阻止事件冒泡
    
    const container = document.querySelector(`[data-code-id="${codeId}"]`);
    if (!container) return;
    
    const codeElement = container.querySelector('code');
    if (!codeElement) return;
    
    // 获取纯文本代码
    const code = codeElement.textContent || codeElement.innerText;
    
    // 检测危险操作
    const dangers = detectDangerousOperations(code);
    if (dangers.length > 0) {
        showDangerConfirm(dangers, () => {
            executePythonCode(code, codeId);
        });
        return;
    }
    
    executePythonCode(code, codeId);
}

// 实际执行Python代码
function executePythonCode(code, codeId) {
    console.log('[Frontend] 运行Python代码, codeId:', codeId);
    
    // 发送给C#执行
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({
            type: 'runPython',
            code: code,
            codeId: codeId
        });
    }
}

// 显示危险操作确认对话框
function showDangerConfirm(dangers, onConfirm) {
    const dialog = document.getElementById('confirmDialog');
    const titleSpan = dialog.querySelector('.confirm-dialog-title span:last-child');
    const body = document.getElementById('confirmDialogBody');
    const confirmBtn = document.getElementById('confirmDialogConfirm');
    const cancelBtn = document.getElementById('confirmDialogCancel');
    
    // 设置标题和内容
    titleSpan.textContent = '危险操作警告';
    body.innerHTML = `
        <div style="color:#dc2626;margin-bottom:8px;">⚠️ 检测到以下可能的危险操作：</div>
        <ul style="margin:0 0 8px 16px;padding:0;font-size:11px;color:#6b7280;">
            ${dangers.map(d => `<li>${d}</li>`).join('')}
        </ul>
        <div style="font-size:11px;color:#92400e;background:#fef3c7;padding:6px 8px;border-radius:4px;">
            💡 数据无价，请确认代码安全后再执行！
        </div>
    `;
    confirmBtn.textContent = '确认执行';
    confirmBtn.style.background = '#dc2626';
    
    // 显示对话框
    dialog.classList.add('show');
    
    // 绑定事件
    const handleConfirm = () => {
        dialog.classList.remove('show');
        confirmBtn.removeEventListener('click', handleConfirm);
        cancelBtn.removeEventListener('click', handleCancel);
        resetConfirmDialog();
        onConfirm();
    };
    const handleCancel = () => {
        dialog.classList.remove('show');
        confirmBtn.removeEventListener('click', handleConfirm);
        cancelBtn.removeEventListener('click', handleCancel);
        resetConfirmDialog();
    };
    
    confirmBtn.addEventListener('click', handleConfirm);
    cancelBtn.addEventListener('click', handleCancel);
}

// 重置确认对话框为默认样式
function resetConfirmDialog() {
    setTimeout(() => {
        const dialog = document.getElementById('confirmDialog');
        const titleSpan = dialog.querySelector('.confirm-dialog-title span:last-child');
        const confirmBtn = document.getElementById('confirmDialogConfirm');
        titleSpan.textContent = '确认删除';
        confirmBtn.textContent = '删除';
        confirmBtn.style.background = '';
    }, 300);
}

// 全局函数: 复制代码
function copyCode(event, codeId) {
    event.stopPropagation(); // 阻止事件冒泡,不触发折叠
    
    const container = document.querySelector(`[data-code-id="${codeId}"]`);
    if (!container) return;
    
    const codeElement = container.querySelector('code');
    if (!codeElement) return;
    
    // 获取纯文本代码(去除HTML标签)
    const code = codeElement.textContent || codeElement.innerText;
    
    // 复制到剪贴板
    if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(code).then(() => {
            // 显示复制成功提示
            const copyBtn = event.target;
            const originalText = copyBtn.textContent;
            copyBtn.textContent = '✔';
            copyBtn.style.color = '#10b981';
            
            setTimeout(() => {
                copyBtn.textContent = originalText;
                copyBtn.style.color = '';
            }, 2000);
        }).catch(err => {
            console.error('复制失败:', err);
            alert('复制失败,请手动复制');
        });
    } else {
        // 降级方案: 使用execCommand
        const textarea = document.createElement('textarea');
        textarea.value = code;
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        document.body.appendChild(textarea);
        textarea.select();
        
        try {
            document.execCommand('copy');
            const copyBtn = event.target;
            const originalText = copyBtn.textContent;
            copyBtn.textContent = '✔';
            copyBtn.style.color = '#10b981';
            
            setTimeout(() => {
                copyBtn.textContent = originalText;
                copyBtn.style.color = '';
            }, 2000);
        } catch (err) {
            console.error('复制失败:', err);
            alert('复制失败,请手动复制');
        } finally {
            document.body.removeChild(textarea);
        }
    }
}

document.addEventListener('DOMContentLoaded', () => {
    // 配置marked库
    if (typeof marked !== 'undefined') {
        // 自定义renderer,实现代码块容器
        const renderer = new marked.Renderer();
        
        renderer.code = function(code, language) {
            const lang = language || 'text';
            const codeId = 'code-' + Math.random().toString(36).substr(2, 9);
            const langDisplay = lang.charAt(0).toUpperCase() + lang.slice(1);
            const isPython = lang.toLowerCase() === 'python' || lang.toLowerCase() === 'py';
            
            // 代码高亮
            let highlightedCode = code;
            if (typeof hljs !== 'undefined') {
                try {
                    if (lang && hljs.getLanguage(lang)) {
                        highlightedCode = hljs.highlight(code, { language: lang }).value;
                    } else {
                        highlightedCode = hljs.highlightAuto(code).value;
                    }
                } catch (err) {
                    console.error('代码高亮失败:', err);
                    highlightedCode = escapeHtml(code);
                }
            } else {
                highlightedCode = escapeHtml(code);
            }
            
            // Python代码块添加运行按钮
            const runButton = isPython ? `<button class="code-block-run" onclick="runPythonCode(event, '${codeId}')" title="运行代码">▶</button>` : '';
            
            // 返回代码块容器HTML（Python代码块包含结果区域）
            const resultArea = isPython ? `<div class="code-result-area" id="result-${codeId}" style="display:none;"></div>` : '';
            
            return `<div class="code-block-container" data-code-id="${codeId}" data-lang="${lang}"><div class="code-block-header" onclick="toggleCodeBlock('${codeId}')"><span class="code-block-lang">📝 ${langDisplay}</span><div class="code-block-actions">${runButton}<button class="code-block-copy" onclick="copyCode(event, '${codeId}')" title="复制代码">📋</button><span class="code-block-toggle">▼</span></div></div><div class="code-block-content"><pre><code>${highlightedCode}</code></pre></div>${resultArea}</div>`;
        };
        
        // 自定义paragraph renderer
        // 注意：表格内容会自动处理，不需要特殊处理
        renderer.paragraph = function(text) {
            // 如果段落包含表格，使用正常的p标签
            if (text.includes('<table') || text.includes('table-wrapper')) {
                return '<p>' + text + '</p>';
            }
            // 否则使用br分隔
            return text + '<br>';
        };
        
        // 自定义表格渲染器，添加滚动容器
        renderer.table = function(header, body) {
            return `<div class="table-wrapper"><table><thead>${header}</thead><tbody>${body}</tbody></table></div>`;
        };
        
        marked.setOptions({
            renderer: renderer,
            breaks: false,  // 关闭自动换行
            gfm: true,      // 启用GFM以支持表格
            tables: true    // 启用表格支持
        });
    }
    
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    const chatList = document.getElementById('chatList');
    const inputText = document.getElementById('input-text');
    const modelSelect = document.getElementById('modelSelect');
    const historyBtn = document.getElementById('historyBtn');
    const historyPanel = document.getElementById('historyPanel');
    const historyList = document.getElementById('historyList');
    const closeHistoryBtn = document.getElementById('closeHistoryBtn');
    const settingsBtn = document.getElementById('settingsBtn');
    const newBtn = document.getElementById('newBtn');
    const sendBtn = document.getElementById('sendBtn');
    const modeToggle = document.getElementById('modeToggle');
    const confirmDialog = document.getElementById('confirmDialog');
    const confirmDialogSessionName = document.getElementById('confirmDialogSessionName');
    const confirmDialogCancel = document.getElementById('confirmDialogCancel');
    const confirmDialogConfirm = document.getElementById('confirmDialogConfirm');
    const toolApprovalDialog = document.getElementById('toolApprovalDialog');
    const toolApprovalBody = document.getElementById('toolApprovalBody');
    const toolApprovalCancelBtn = document.getElementById('toolApprovalCancelBtn');
    const toolApprovalAllowOnceBtn = document.getElementById('toolApprovalAllowOnceBtn');
    const toolApprovalAllowBtn = document.getElementById('toolApprovalAllowBtn');
    const imageUploadBtn = document.getElementById('imageUploadBtn');
    const imageInput = document.getElementById('imageInput');
    const imagePreviewContainer = document.getElementById('imagePreviewContainer');
    const scrollToBottomBtn = document.getElementById('scrollToBottomBtn');

    let isStreaming = false;
    let currentAiMsgContent = null;
    let currentSessionId = null;
    let pendingDeleteSession = null;
    let currentMode = 'chat'; // 'chat' or 'agent'
    let currentStreamText = ''; // 用于累积流式输出的文本
    let currentTurnId = null;
    const turnMessageMap = new Map();
    const turnTextMap = new Map();
    let selectedImages = []; // 存储选中的图片(base64)
    let currentModelSupportsVision = false; // 当前模型是否支持视觉
    let pythonExecutionHistory = new Map(); // 存储Python执行历史 (code -> execData)
    let pendingToolApprovalRequestId = null;

    // 自动调整高度 & 按钮状态
    inputText.addEventListener('input', function() {
        this.style.height = 'auto';
        this.style.height = Math.min(this.scrollHeight, 150) + 'px';
        toggleSendBtn();
    });

    function toggleSendBtn() {
        if (isStreaming) {
            // 流式输出中，显示停止按钮
            sendBtn.textContent = '⬛';
            sendBtn.title = '停止';
            sendBtn.disabled = false;
            sendBtn.classList.add('stopping');
        } else {
            // 正常状态，显示发送按钮
            sendBtn.textContent = '↵';
            sendBtn.title = '发送';
            sendBtn.disabled = !inputText.value.trim();
            sendBtn.classList.remove('stopping');
        }
    }

    // 初始化按钮状态
    toggleSendBtn();

    // 监听回车
    inputText.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });
    
    // 监听发送按钮
    sendBtn.addEventListener('click', sendMessage);
    
    // 图片上传按钮事件
    imageUploadBtn.addEventListener('click', () => {
        imageInput.click();
    });
    
    // 图片选择事件
    imageInput.addEventListener('change', handleImageSelect);
    
    // 模型切换事件
    modelSelect.addEventListener('change', handleModelChange);

    // 监听聊天列表滚动
    let scrollTimeout;
    chatList.addEventListener('scroll', () => {
        // 防抖，避免频繁更新
        clearTimeout(scrollTimeout);
        scrollTimeout = setTimeout(() => {
            updateScrollButton();
        }, 100);
    });
    
    // 回到底部按钮点击事件
    scrollToBottomBtn.addEventListener('click', forceScrollToBottom);

    function sendMessage() {
        // 如果正在流式输出，则停止
        if (isStreaming) {
            console.log('[Frontend] 发送停止请求');
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({
                    type: 'stopMessage'
                });
            }
            return;
        }

        const text = inputText.value.trim();
        if (!text) return;

        // 检查是否已配置模型
        if (!modelSelect.value) {
            showSystemNotice('请先在设置中配置AI模型。点击右上角 ⚙️ 按钮打开设置。');
            return;
        }

        console.log('[Frontend] sendMessage 被调用, 消息:', text, '图片数量:', selectedImages.length);

        // 发送新消息时强制滚动到底部
        forceScrollToBottom();

        // 收集Python执行引用（在显示消息前收集）
        let execRefsForDisplay = null;
        if (window.pythonExecReferences && window.pythonExecReferences.size > 0) {
            execRefsForDisplay = [];
            window.pythonExecReferences.forEach((data, id) => {
                execRefsForDisplay.push({
                    id: id,
                    code: data.code,
                    success: data.success
                });
            });
        }

        // 添加用户消息(包含图片和引用)
        appendMessage('user', text, false, selectedImages.length > 0 ? selectedImages : null, execRefsForDisplay);
        
        // 清空输入
        inputText.value = '';
        inputText.style.height = 'auto';
        toggleSendBtn();

        // 收集Python执行引用（发送给后端）
        let execReferences = null;
        if (window.pythonExecReferences && window.pythonExecReferences.size > 0) {
            execReferences = [];
            window.pythonExecReferences.forEach((data, id) => {
                execReferences.push({
                    id: id,
                    output: data.output,
                    error: data.error,
                    success: data.success
                });
            });
        }

        // 发送给C#
        if (window.chrome && window.chrome.webview) {
            console.log('[Frontend] 发送消息到C#, 模式:', currentMode, '引用数量:', execReferences?.length || 0);
            window.chrome.webview.postMessage({
                type: 'sendMessage',
                message: text,
                model: modelSelect.value,
                mode: currentMode,
                images: selectedImages.length > 0 ? selectedImages : null,
                execReferences: execReferences
            });
        }
        
        // 清空图片和引用
        clearImages();
        clearExecReferences();
    }
    
    // 清空执行引用
    function clearExecReferences() {
        if (window.pythonExecReferences) {
            window.pythonExecReferences.clear();
        }
        const previewContainer = document.getElementById('imagePreviewContainer');
        if (previewContainer) {
            // 移除所有引用标签
            const refTags = previewContainer.querySelectorAll('.exec-ref-tag');
            refTags.forEach(tag => tag.remove());
            // 如果没有其他内容，隐藏容器
            if (previewContainer.children.length === 0) {
                previewContainer.style.display = 'none';
            }
        }
    }

    function showWelcomeMessage() {
        chatList.innerHTML = '';
        scrollToBottomBtn.style.display = 'none';
        appendMessage('ai', '你好！我是你的GIS助手，有什么可以帮你的吗？');
    }

    // 显示系统提示（非对话消息，用于提醒用户操作）
    function showSystemNotice(message) {
        const div = document.createElement('div');
        div.style.cssText = 'text-align:center;padding:12px 16px;margin:8px 0;background:#fef3c7;border-radius:8px;font-size:12px;color:#92400e;';
        div.textContent = message;
        chatList.appendChild(div);
        forceScrollToBottom();
    }

    function resolveTurnId(rawTurnId) {
        if (rawTurnId) return rawTurnId;
        if (currentTurnId) return currentTurnId;
        const fallback = `turn-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
        currentTurnId = fallback;
        return fallback;
    }

    function ensureTurnContext(turnId) {
        if (!turnId) return null;

        let targetAiMsg = turnMessageMap.get(turnId);
        if (!targetAiMsg) {
            targetAiMsg = appendMessage('ai', '', true);
            turnMessageMap.set(turnId, targetAiMsg);
            if (!turnTextMap.has(turnId)) {
                turnTextMap.set(turnId, '');
            }
        }

        return targetAiMsg;
    }

    function finalizeTurnMessage(turnId) {
        const targetAiMsg = turnMessageMap.get(turnId);
        if (!targetAiMsg || !targetAiMsg.content) return;

        const finalText = turnTextMap.get(turnId) || '';
        if (finalText) {
            targetAiMsg.content.innerHTML = formatContent(finalText);
            const spinners = targetAiMsg.content.querySelectorAll('.loading-spinner');
            spinners.forEach(spinner => spinner.remove());
        }
    }

    function upsertToolCallCard(targetAiMsg, data) {
        if (!targetAiMsg || !targetAiMsg.toolCallsContainer) {
            return;
        }

        const callId = data.callId || (`call-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`);
        const toolName = data.toolName || 'tool';
        const status = data.status || 'running';
        const message = data.message || '';
        const preview = data.preview || '';
        const title = getToolDisplayName(toolName);
        const icon = status === 'success' ? '🌐' : (status === 'failed' ? '⚠️' : '⏳');

        if (!targetAiMsg.toolCallMap) {
            targetAiMsg.toolCallMap = new Map();
        }
        if (!targetAiMsg.toolCallDataMap) {
            targetAiMsg.toolCallDataMap = new Map();
        }

        const mergedData = {
            ...(targetAiMsg.toolCallDataMap.get(callId) || {}),
            ...data,
            callId
        };
        targetAiMsg.toolCallDataMap.set(callId, mergedData);

        let card = targetAiMsg.toolCallMap.get(callId);
        if (!card) {
            card = document.createElement('div');
            card.className = 'tool-call-item';
            card.dataset.callId = callId;
            card.innerHTML = `
                <div class="tool-call-item-header">
                    <span class="tool-call-toggle">▼</span>
                    <span class="tool-call-icon"></span>
                    <span class="tool-call-title"></span>
                    <span class="tool-call-status"></span>
                    <span class="tool-call-actions">
                        <button type="button" class="tool-call-export" title="导出为Excel">导出Excel</button>
                    </span>
                </div>
                <div class="tool-call-item-body"></div>`;

            const header = card.querySelector('.tool-call-item-header');
            header.addEventListener('click', () => {
                card.classList.toggle('collapsed');
            });

            const exportBtn = card.querySelector('.tool-call-export');
            if (exportBtn) {
                exportBtn.addEventListener('click', (event) => {
                    event.stopPropagation();
                    exportToolCall(targetAiMsg, callId);
                });
            }

            targetAiMsg.toolCallsContainer.appendChild(card);
            targetAiMsg.toolCallMap.set(callId, card);

            if (targetAiMsg.root) {
                targetAiMsg.root.classList.add('has-tool-calls');
            }
        }

        card.classList.remove('running', 'success', 'failed');
        card.classList.add(status);

        const iconEl = card.querySelector('.tool-call-icon');
        const titleEl = card.querySelector('.tool-call-title');
        const statusEl = card.querySelector('.tool-call-status');
        const bodyEl = card.querySelector('.tool-call-item-body');

        if (iconEl) iconEl.textContent = icon;
        if (titleEl) titleEl.textContent = title;
        if (statusEl) statusEl.textContent = message;
        if (bodyEl) {
            bodyEl.innerHTML = preview.trim()
                ? `<pre class="tool-call-preview">${escapeHtml(preview)}</pre>`
                : '<span class="tool-call-empty">暂无详情</span>';
        }

        if (status === 'running') {
            card.classList.remove('collapsed');
        } else if (status === 'success') {
            card.classList.add('collapsed');
        } else {
            card.classList.remove('collapsed');
        }

        scrollToBottom();
    }

    function exportToolCall(targetAiMsg, callId) {
        if (!window.chrome?.webview) {
            return;
        }

        const toolData = targetAiMsg?.toolCallDataMap?.get(callId);
        if (!toolData) {
            appendMessage('ai', '⚠️ 未找到可导出的工具结果。');
            return;
        }

        window.chrome.webview.postMessage({
            type: 'exportToolCall',
            payload: {
                callId,
                turnId: toolData.turnId || currentTurnId || '',
                toolName: toolData.toolName || 'tool',
                status: toolData.status || '',
                message: toolData.message || '',
                preview: toolData.preview || '',
                parameters: toolData.parameters || '',
                result: toolData.result || '',
                timestamp: new Date().toISOString()
            }
        });
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

    function getToolDisplayName(toolName) {
        const map = {
            web_fetch: '网页抓取',
            project_snapshot: '工程快照',
            list_map_layers: '图层列表',
            describe_layer_schema: '图层结构',
            selection_summary: '选择集摘要',
            layer_query: '记录查询',
            field_profile: '字段画像',
            overlay_intersect_summary: '叠加汇总',
            buffer_analysis: '缓冲区分析',
            clip_analysis: '裁剪分析'
        };

        return map[toolName] || toolName || 'tool';
    }

    function mapHistoryToolStatus(status) {
        const normalized = (status || '').toLowerCase();
        if (normalized === 'success') return 'success';
        if (normalized === 'failed' || normalized === 'error' || normalized === 'denied' || normalized === 'cancel') return 'failed';
        return 'running';
    }

    function tryParseJson(text) {
        if (!text || typeof text !== 'string') return null;
        try {
            return JSON.parse(text);
        } catch {
            return null;
        }
    }

    function truncateText(text, maxLength) {
        if (!text) return '';
        if (text.length <= maxLength) return text;
        return text.substring(0, maxLength) + '...';
    }

    function parseReplayTimestamp(value) {
        if (value === null || value === undefined) return null;

        if (typeof value === 'number') {
            return Number.isFinite(value) ? value : null;
        }

        if (value instanceof Date) {
            const t = value.getTime();
            return Number.isFinite(t) ? t : null;
        }

        const raw = String(value).trim();
        if (!raw) return null;

        let t = Date.parse(raw);
        if (Number.isFinite(t)) return t;

        const normalized = raw.includes(' ') && !raw.includes('T') ? raw.replace(' ', 'T') : raw;
        t = Date.parse(normalized);
        if (Number.isFinite(t)) return t;

        if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$/.test(normalized)) {
            t = Date.parse(normalized + 'Z');
            if (Number.isFinite(t)) return t;
        }

        return null;
    }

    function buildToolHistoryMessage(tool) {
        const status = mapHistoryToolStatus(tool?.status);
        const parsed = tryParseJson(tool?.result);

        if (parsed && typeof parsed === 'object') {
            const msg = parsed.Message || parsed.message || parsed.Error || parsed.error;
            if (msg && String(msg).trim()) {
                return String(msg).trim();
            }
        }

        if (status === 'success') return '执行完成';
        if (status === 'failed') return '执行失败';
        return '执行中';
    }

    function buildToolHistoryPreview(tool) {
        const blocks = [];

        const parsedParams = tryParseJson(tool?.parameters);
        if (parsedParams && typeof parsedParams === 'object') {
            blocks.push(`执行参数:\n${JSON.stringify(parsedParams, null, 2)}`);
        } else if (tool?.parameters && String(tool.parameters).trim()) {
            blocks.push(`执行参数:\n${String(tool.parameters).trim()}`);
        }

        const parsedResult = tryParseJson(tool?.result);
        if (parsedResult && typeof parsedResult === 'object') {
            if (parsedResult.Data !== undefined || parsedResult.data !== undefined) {
                const dataObj = parsedResult.Data !== undefined ? parsedResult.Data : parsedResult.data;
                blocks.push(`执行结果:\n${JSON.stringify(dataObj, null, 2)}`);
            } else {
                blocks.push(`执行结果:\n${JSON.stringify(parsedResult, null, 2)}`);
            }
        } else if (tool?.result && String(tool.result).trim()) {
            blocks.push(`执行结果:\n${String(tool.result).trim()}`);
        }

        if (blocks.length === 0) {
            return '';
        }

        return truncateText(blocks.join('\n\n'), 3000);
    }

    function findAssistantForToolByTime(toolTimestamp, assistantTimeline) {
        if (!assistantTimeline || assistantTimeline.length === 0) {
            return null;
        }

        if (!Number.isFinite(toolTimestamp)) {
            return assistantTimeline[assistantTimeline.length - 1].msg;
        }

        let target = null;
        for (let i = 0; i < assistantTimeline.length; i++) {
            const item = assistantTimeline[i];
            if (Number.isFinite(item.timestamp) && item.timestamp <= toolTimestamp) {
                target = item.msg;
                continue;
            }

            if (target) {
                break;
            }
        }

        return target || assistantTimeline[assistantTimeline.length - 1].msg;
    }

    function replayToolHistory(messages, toolCalls) {
        if ((!messages || messages.length === 0) && (!toolCalls || toolCalls.length === 0)) {
            return;
        }

        const turnMessageMap = new Map();
        const assistantTimeline = [];
        let lastAssistantMsg = null;

        (messages || []).forEach(msg => {
            let images = null;
            if (msg.images && msg.images.trim() !== '') {
                try {
                    images = JSON.parse(msg.images);
                } catch (e) {
                    console.warn('解析图片JSON失败:', e);
                }
            }

            if (msg.role === 'user') {
                appendMessage('user', msg.content, false, images);
                return;
            }

            const hasThinking = msg.thinking && msg.thinking.trim() !== '';
            const aiMsg = appendMessage('ai', msg.content, hasThinking, images);
            if (hasThinking && aiMsg && aiMsg.thinking) {
                aiMsg.thinkingContainer.style.display = 'block';
                aiMsg.thinking.innerHTML = escapeHtml(msg.thinking).replace(/\n/g, '<br>');
                if (aiMsg.thinkingTitle) {
                    aiMsg.thinkingTitle.textContent = '思考过程（点击展开）';
                }
            }

            const turnId = (msg.turnId || '').trim();
            if (turnId && !turnMessageMap.has(turnId)) {
                turnMessageMap.set(turnId, aiMsg);
            }

            assistantTimeline.push({
                msg: aiMsg,
                timestamp: parseReplayTimestamp(msg.timestamp)
            });
            lastAssistantMsg = aiMsg;
        });

        const sortedToolCalls = [...(toolCalls || [])].sort((a, b) => {
            const aTime = parseReplayTimestamp(a.timestamp);
            const bTime = parseReplayTimestamp(b.timestamp);
            if (Number.isFinite(aTime) && Number.isFinite(bTime) && aTime !== bTime) {
                return aTime - bTime;
            }

            const aId = Number.isFinite(Number(a.id)) ? Number(a.id) : 0;
            const bId = Number.isFinite(Number(b.id)) ? Number(b.id) : 0;
            return aId - bId;
        });

        sortedToolCalls.forEach((tool, index) => {
            const turnId = (tool.turnId || '').trim();
            let targetAiMsg = turnId ? turnMessageMap.get(turnId) : null;

            if (!targetAiMsg) {
                targetAiMsg = findAssistantForToolByTime(parseReplayTimestamp(tool.timestamp), assistantTimeline) || lastAssistantMsg;
            }

            if (!targetAiMsg) {
                targetAiMsg = appendMessage('ai', '', false);
                assistantTimeline.push({ msg: targetAiMsg, timestamp: parseReplayTimestamp(tool.timestamp) });
                lastAssistantMsg = targetAiMsg;
            }

            if (turnId && !turnMessageMap.has(turnId)) {
                turnMessageMap.set(turnId, targetAiMsg);
            }

            upsertToolCallCard(targetAiMsg, {
                callId: tool.callId || `history-tool-${tool.id || index}`,
                toolName: tool.toolName,
                status: mapHistoryToolStatus(tool.status),
                message: buildToolHistoryMessage(tool),
                preview: buildToolHistoryPreview(tool),
                parameters: tool.parameters || '',
                result: tool.result || '',
                turnId
            });
        });
    }

    function appendMessage(role, text, hasThinking = false, images = null, execRefs = null) {
        const div = document.createElement('div');
        div.className = `msg-item ${role}`;
        
        const roleLabel = role === 'user' ? 'USER' : 'AI';
        
        let html = `
            <div class="msg-meta">
                <span class="role-badge">${roleLabel}</span>
            </div>`;
        
        // AI消息可能包含思考模块
        if (role === 'ai' && hasThinking) {
            html += `
                <div class="thinking-container" style="display:none;">
                    <div class="thinking-header" onclick="this.parentElement.classList.toggle('expanded')">
                        <span class="thinking-icon">💭</span>
                        <span class="thinking-title">思考中...</span>
                        <span class="thinking-toggle">▼</span>
                    </div>
                    <div class="thinking-content">
                        <span class="thinking-loading">正在思考...</span>
                    </div>
                </div>`;
        }
        
        // 构建消息内容，将图片放在msg-content内部
        let contentHtml = '<div class="msg-content">';
        
        // 如果有图片，先显示图片
        if (images && images.length > 0) {
            contentHtml += '<div class="msg-images">';
            images.forEach(img => {
                contentHtml += `<img src="${img}" class="msg-image" />`;
            });
            contentHtml += '</div>';
        }
        
        // 如果有执行引用，显示引用标签
        if (execRefs && execRefs.length > 0) {
            contentHtml += '<div class="msg-exec-refs">';
            execRefs.forEach(ref => {
                const statusIcon = ref.success ? '✅' : '❌';
                const codePreview = (ref.code || '').substring(0, 25) + (ref.code?.length > 25 ? '...' : '');
                contentHtml += `<span class="msg-exec-ref-tag" title="${escapeHtml(codePreview)}">${statusIcon} @执行#${ref.id}</span>`;
            });
            contentHtml += '</div>';
        }
        
        // 然后显示文本内容
        contentHtml += text ? formatContent(text) : '';
        contentHtml += '</div>';
        
        html += contentHtml;

        // 工具调用模块放在回复文本后，保持阅读顺序
        if (role === 'ai') {
            html += '<div class="tool-calls-container"></div>';
        }
        
        div.innerHTML = html;
        chatList.appendChild(div);
        scrollToBottom();
        
        if (role === 'ai') {
            return {
                root: div,
                content: div.querySelector('.msg-content'),
                thinking: div.querySelector('.thinking-content'),
                thinkingContainer: div.querySelector('.thinking-container'),
                thinkingTitle: div.querySelector('.thinking-title'),
                toolCallsContainer: div.querySelector('.tool-calls-container'),
                toolCallMap: new Map(),
                toolCallDataMap: new Map(),
                hasCollapsedThinking: false
            };
        }
    }

    // 增量更新内容(用于代码块输出中)
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
    
    // 移除模型特殊标记
    function removeModelTokens(text) {
        if (!text) return '';
        
        // 常见的模型特殊标记
        const tokens = [
            /<\|begin_of_box\|>/g,
            /<\|end_of_box\|>/g,
            /<\|begin_of_text\|>/g,
            /<\|end_of_text\|>/g,
            /<\|start_header_id\|>/g,
            /<\|end_header_id\|>/g,
            /<\|eot_id\|>/g,
            /<\|im_start\|>/g,
            /<\|im_end\|>/g,
            /<\|system\|>/g,
            /<\|user\|>/g,
            /<\|assistant\|>/g,
            /<think>/g,
            /<\/think>/g
        ];
        
        // 移除所有特殊标记
        tokens.forEach(token => {
            text = text.replace(token, '');
        });
        
        return text;
    }
    
    // 格式化内容 - 手动处理Markdown完全控制换行
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
    
    // 简单的文本格式化(不产生额外空行)
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
    
    // 处理行内格式(粗体、斜体、行内代码)
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
    
    // 检查是否在底部
    function isScrolledToBottom() {
        const threshold = 100; // 允许100px的误差，避免小幅度滚动就停止自动滚动
        return chatList.scrollHeight - chatList.scrollTop - chatList.clientHeight < threshold;
    }
    
    // 更新回到底部按钮的显示状态
    function updateScrollButton() {
        if (isScrolledToBottom()) {
            scrollToBottomBtn.style.display = 'none';
        } else {
            scrollToBottomBtn.style.display = 'flex';
        }
    }
    
    // 强制滚动到底部
    function forceScrollToBottom() {
        chatList.scrollTop = chatList.scrollHeight;
        scrollToBottomBtn.style.display = 'none';
    }

    // 填充模型选择器
    function populateModelSelect(models) {
        // 清空现有选项
        modelSelect.innerHTML = '';
        
        if (!models || models.length === 0) {
            // 空状态：提示用户去设置
            const option = document.createElement('option');
            option.value = '';
            option.textContent = '未配置模型 - 请点击设置';
            option.disabled = true;
            option.selected = true;
            modelSelect.appendChild(option);
            modelSelect.style.color = '#9ca3af';
            return;
        }
        
        modelSelect.style.color = '';
        
        // 添加模型选项
        models.forEach(model => {
            const option = document.createElement('option');
            option.value = model.value;
            option.textContent = model.label;
            option.dataset.supportsVision = model.supportsVision || false;
            if (model.isDefault) {
                option.selected = true;
            }
            modelSelect.appendChild(option);
        });
        
        // 更新当前模型的视觉支持状态
        handleModelChange();
    }

    // 历史对话相关函数
    function loadHistorySessions(sessions) {
        historyList.innerHTML = '';
        
        if (!sessions || sessions.length === 0) {
            historyList.innerHTML = '<div class="history-empty">暂无历史对话</div>';
            return;
        }
        
        sessions.forEach(session => {
            const item = document.createElement('div');
            item.className = 'history-item';
            item.dataset.sessionId = session.sessionId;
            if (session.sessionId === currentSessionId) {
                item.classList.add('active');
            }
            
            const createdTime = formatTime(session.createdAt);
            const lastTime = formatTime(session.lastActivity);
            const timeDisplay = createdTime === lastTime ? createdTime : `${createdTime} · ${lastTime}`;
            
            item.innerHTML = `
                <div class="history-item-content">
                    <div class="history-item-title">${escapeHtml(session.title || '未命名对话')}</div>
                    <div class="history-item-time">${timeDisplay}</div>
                </div>
                <button class="history-item-delete" title="删除会话">×</button>
            `;
            
            // 点击会话项加载会话
            const content = item.querySelector('.history-item-content');
            content.addEventListener('click', () => {
                loadSession(session.sessionId);
                historyPanel.classList.remove('show');
            });
            
            // 点击删除按钮
            const deleteBtn = item.querySelector('.history-item-delete');
            deleteBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                deleteSession(session.sessionId, session.title);
            });
            
            historyList.appendChild(item);
        });
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
    
    function loadSession(sessionId) {
        if (window.chrome?.webview) {
            window.chrome.webview.postMessage({
                type: 'loadSession',
                sessionId: sessionId
            });
        }
    }
    
    function deleteSession(sessionId, title) {
        // 显示自定义确认对话框
        pendingDeleteSession = { sessionId, title };
        confirmDialogSessionName.textContent = `"${title || '未命名对话'}"`;
        confirmDialog.classList.add('show');
    }
    
    function confirmDelete() {
        if (pendingDeleteSession && window.chrome?.webview) {
            window.chrome.webview.postMessage({
                type: 'deleteSession',
                sessionId: pendingDeleteSession.sessionId
            });
        }
        closeConfirmDialog();
    }
    
    function closeConfirmDialog() {
        confirmDialog.classList.remove('show');
        pendingDeleteSession = null;
    }

    function showToolApprovalDialog(data) {
        if (!toolApprovalDialog || !toolApprovalBody) return;

        pendingToolApprovalRequestId = data.requestId || null;

        const toolNames = Array.isArray(data.toolNames) ? data.toolNames : [];
        const callCount = Number(data.callCount || 0);
        const toolListHtml = toolNames.length > 0
            ? `<ul style="margin:6px 0 0 18px;padding:0;color:#4b5563;font-size:12px;line-height:1.5;">${toolNames.map(name => `<li>${escapeHtml(name)}</li>`).join('')}</ul>`
            : '';

        toolApprovalBody.innerHTML = `
            <div style="margin-bottom:8px;color:#111827;">AI 请求调用工具执行任务。</div>
            <div style="font-size:12px;color:#6b7280;">调用次数：${callCount > 0 ? callCount : '未知'}</div>
            ${toolListHtml}
            <div style="margin-top:10px;padding:8px 10px;background:#eff6ff;border-radius:6px;color:#1e3a8a;font-size:12px;">
                选择“允许”后，将自动切换为不再提示（可在设置中改回）。
            </div>
        `;

        toolApprovalDialog.classList.add('show');
    }

    function sendToolApprovalDecision(decision) {
        if (!pendingToolApprovalRequestId) return;

        if (window.chrome?.webview) {
            window.chrome.webview.postMessage({
                type: 'toolApprovalResponse',
                requestId: pendingToolApprovalRequestId,
                decision: decision
            });
        }

        pendingToolApprovalRequestId = null;
        toolApprovalDialog?.classList.remove('show');
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

    // C# 消息处理
    window.addEventListener('message', (event) => {
        const data = event.data;
        if (!data || !data.type) return;
        
        switch (data.type) {
            case 'modelList':
                // 收到模型列表，填充下拉框
                populateModelSelect(data.models);
                break;
            case 'sessionInfo':
                // 收到当前会话信息
                currentSessionId = data.sessionId;
                if (data.isNewSession) {
                    showWelcomeMessage();
                }
                // 更新历史列表中的active状态
                updateHistoryActiveState();
                break;
            case 'toolPolicy':
                updateSensitiveModeBanner(data.sensitiveMode);
                break;
            case 'historySessions':
                // 收到历史会话列表
                loadHistorySessions(data.sessions);
                break;
            case 'sessionDeleted':
                // 会话删除成功
                // 如果删除的是当前会话,创建新会话
                if (data.deletedSessionId === currentSessionId) {
                    if (window.chrome?.webview) {
                        window.chrome.webview.postMessage({ type: 'newSession' });
                    }
                }
                // 刷新历史列表
                if (historyPanel.classList.contains('show')) {
                    if (window.chrome?.webview) {
                        window.chrome.webview.postMessage({ type: 'getHistorySessions' });
                    }
                }
                break;
            case 'sessionLoaded':
                // 会话加载完成,刷新界面
                chatList.innerHTML = '';
                currentSessionId = data.sessionId;
                scrollToBottomBtn.style.display = 'none';
                
                console.log('[sessionLoaded] 加载消息数量:', data.messages?.length);
                console.log('[sessionLoaded] Python执行记录数量:', data.pythonExecutions?.length);
                
                // 保存Python执行历史（以代码内容为key）
                pythonExecutionHistory.clear();
                if (data.pythonExecutions && data.pythonExecutions.length > 0) {
                    data.pythonExecutions.forEach(exec => {
                        // 使用代码内容作为key
                        const codeKey = exec.code.trim();
                        pythonExecutionHistory.set(codeKey, {
                            id: exec.id,
                            codeId: exec.codeId,
                            code: exec.code,
                            output: exec.output,
                            error: exec.error,
                            success: exec.success,
                            executionTime: exec.executionTime,
                            timestamp: exec.timestamp
                        });
                    });
                }
                
                // 显示历史消息与工具调用
                if ((data.messages && data.messages.length > 0) || (data.toolCalls && data.toolCalls.length > 0)) {
                    replayToolHistory(data.messages || [], data.toolCalls || []);
                    
                    // 在消息加载完成后，恢复Python执行结果
                    setTimeout(() => {
                        restorePythonExecutionResults();
                    }, 100);
                } else {
                    // 如果没有历史消息，显示欢迎消息
                    showWelcomeMessage();
                }
                
                // 更新历史列表中的active状态
                updateHistoryActiveState();
                break;
            case 'streamStart':
                isStreaming = true;
                currentStreamText = ''; // 重置累积文本
                toggleSendBtn();
                currentTurnId = null;
                turnMessageMap.clear();
                turnTextMap.clear();
                currentAiMsgContent = null;
                break;
            case 'assistantTurnStart':
                {
                const turnId = resolveTurnId(data.turnId);
                currentTurnId = turnId;
                const targetAiMsg = ensureTurnContext(turnId);
                currentAiMsgContent = targetAiMsg;
                turnTextMap.set(turnId, '');
                }
                break;
            case 'assistantTurnEnd':
                {
                const turnId = resolveTurnId(data.turnId);
                finalizeTurnMessage(turnId);
                break;
                }
            case 'streamThinking':
                {
                const turnId = resolveTurnId(data.turnId);
                const targetAiMsg = ensureTurnContext(turnId);
                if (!targetAiMsg) break;

                // 收到思考内容
                if (targetAiMsg.thinking) {
                    // 第一次收到思考内容，显示并展开思考模块
                    if (targetAiMsg.thinkingContainer.style.display === 'none') {
                        targetAiMsg.thinkingContainer.style.display = 'block';
                        targetAiMsg.thinkingContainer.classList.add('expanded');
                        
                        // 更新标题为"思考过程"
                        if (targetAiMsg.thinkingTitle) {
                            targetAiMsg.thinkingTitle.textContent = '思考过程';
                        }
                        
                        // 移除"正在思考..."占位符
                        const loading = targetAiMsg.thinking.querySelector('.thinking-loading');
                        if (loading) loading.remove();
                    }
                    
                    // 追加思考内容
                    targetAiMsg.thinking.innerHTML += escapeHtml(data.content).replace(/\n/g, '<br>');
                    
                    // 思考模块内部滚动到底部
                    targetAiMsg.thinking.scrollTop = targetAiMsg.thinking.scrollHeight;
                    
                    // 整个聊天列表也滚动到底部
                    scrollToBottom();
                }
                }
                break;
            case 'toolCall':
                {
                const turnId = resolveTurnId(data.turnId);
                const targetAiMsg = ensureTurnContext(turnId);
                upsertToolCallCard(targetAiMsg, data);
                }
                break;
            case 'toolExported':
                appendMessage('ai', `✅ 工具结果已导出：${data.fileName || data.filePath || ''}`);
                break;
            case 'toolExportFailed':
                appendMessage('ai', `❌ 工具结果导出失败：${data.message || '未知错误'}`);
                break;
            case 'toolApprovalRequest':
                showToolApprovalDialog(data);
                break;
            case 'streamChunk':
                {
                const turnId = resolveTurnId(data.turnId);
                const targetAiMsg = ensureTurnContext(turnId);
                if (!targetAiMsg || !targetAiMsg.content) break;

                currentAiMsgContent = targetAiMsg;

                    // 收到第一个回复chunk时，自动收起思考模块
                    if (!targetAiMsg.hasCollapsedThinking && targetAiMsg.thinkingContainer && 
                        targetAiMsg.thinkingContainer.classList.contains('expanded')) {
                        if (targetAiMsg.thinkingTitle) {
                            targetAiMsg.thinkingTitle.textContent = '思考过程（点击展开）';
                        }
                        targetAiMsg.thinkingContainer.classList.remove('expanded');
                        targetAiMsg.hasCollapsedThinking = true;
                    }
                    
                    // 累积文本
                    const currentText = turnTextMap.get(turnId) || '';
                    const nextText = currentText + data.content;
                    turnTextMap.set(turnId, nextText);
                    currentStreamText = nextText;
                    
                    // 检测是否在代码块中
                    const inCodeBlock = (nextText.match(/```/g) || []).length % 2 === 1;
                    
                    if (inCodeBlock) {
                        // 在代码块中 - 智能追加模式
                        updateContentIncremental(nextText);
                    } else {
                        // 不在代码块中 - 完整重渲染
                        targetAiMsg.content.innerHTML = formatContent(nextText);
                    }
                    
                    scrollToBottom();
                }
                break;
            case 'streamEnd':
                {
                isStreaming = false;
                toggleSendBtn();
                pendingToolApprovalRequestId = null;
                toolApprovalDialog?.classList.remove('show');
                turnTextMap.forEach((_, turnId) => finalizeTurnMessage(turnId));
                turnMessageMap.clear();
                turnTextMap.clear();
                currentTurnId = null;
                currentAiMsgContent = null;
                currentStreamText = '';
                // 更新滚动按钮状态
                updateScrollButton();
                }
                break;
            case 'error':
                appendMessage('ai', `❌ ${data.message}`);
                isStreaming = false;
                pendingToolApprovalRequestId = null;
                toolApprovalDialog?.classList.remove('show');
                turnMessageMap.clear();
                turnTextMap.clear();
                currentTurnId = null;
                currentAiMsgContent = null;
                currentStreamText = '';
                toggleSendBtn();
                break;
            case 'pythonRunning':
                // Python代码正在执行
                handlePythonRunning(data.codeId);
                break;
            case 'pythonResult':
                // Python执行结果
                handlePythonResult(data);
                break;
        }
    });
    
    // 恢复Python执行结果（会话加载后调用）
    function restorePythonExecutionResults() {
        if (pythonExecutionHistory.size === 0) return;
        
        // 查找所有Python代码块
        const pythonCodeBlocks = document.querySelectorAll('.code-block-container[data-lang="python"], .code-block-container[data-lang="py"]');
        console.log('[restorePythonExecutionResults] 找到Python代码块数量:', pythonCodeBlocks.length);
        
        pythonCodeBlocks.forEach(container => {
            const codeElement = container.querySelector('code');
            if (!codeElement) return;
            
            const codeContent = (codeElement.textContent || codeElement.innerText).trim();
            const execData = pythonExecutionHistory.get(codeContent);
            
            if (execData) {
                const codeId = container.dataset.codeId;
                const resultArea = document.getElementById(`result-${codeId}`);
                
                if (resultArea) {
                    // 恢复执行结果
                    resultArea.dataset.execData = JSON.stringify({
                        codeId,
                        executionId: execData.id,
                        success: execData.success,
                        output: execData.output || '',
                        error: execData.error || '',
                        code: execData.code || '',
                        executionTime: execData.executionTime
                    });
                    
                    resultArea.style.display = 'block';
                    resultArea.className = `code-result-area ${execData.success ? 'success' : 'error'}`;
                    
                    const statusIcon = execData.success ? '✅' : '❌';
                    const statusText = execData.success ? '执行成功' : '执行失败';
                    const timeText = execData.executionTime ? ` (${execData.executionTime}ms)` : '';
                    
                    let contentHtml = '';
                    if (execData.output && execData.output.trim()) {
                        contentHtml += `<div class="result-output"><div class="result-output-title">输出:</div><pre>${escapeHtml(execData.output)}</pre></div>`;
                    }
                    if (execData.error && execData.error.trim()) {
                        contentHtml += `<div class="result-error"><div class="result-error-title">错误:</div><pre>${escapeHtml(execData.error)}</pre></div>`;
                    }
                    if (!contentHtml) {
                        contentHtml = `<div class="result-empty">${execData.success ? '执行完成，无输出' : '未知错误'}</div>`;
                    }
                    
                    // 添加历史标记
                    const historyTag = `<span class="result-history-tag" title="${new Date(execData.timestamp).toLocaleString()}">历史</span>`;
                    
                    resultArea.innerHTML = `<div class="result-header" onclick="toggleResultArea('${codeId}')"><span class="result-toggle">▼</span><span class="result-icon">${statusIcon}</span><span class="result-title">${statusText}${timeText}</span>${historyTag}<div class="result-actions"><button class="result-copy" onclick="event.stopPropagation();copyExecutionResult('${codeId}')" title="复制结果">📋</button><button class="result-quote" onclick="event.stopPropagation();quoteExecutionResult('${codeId}')" title="@引用到对话">@</button><button class="result-close" onclick="event.stopPropagation();closeResultArea('${codeId}')" title="关闭">✕</button></div></div><div class="result-content">${contentHtml}</div>`;
                    
                    console.log('[restorePythonExecutionResults] 恢复执行结果:', codeId);
                }
            }
        });
    }
    
    // 处理Python代码执行中状态
    function handlePythonRunning(codeId) {
        const resultArea = document.getElementById(`result-${codeId}`);
        if (!resultArea) return;
        
        resultArea.style.display = 'block';
        resultArea.className = 'code-result-area running';
        resultArea.innerHTML = `<div class="result-header"><span class="result-icon">⏳</span><span class="result-title">执行中...</span></div><div class="result-content"><div class="result-loading"></div></div>`;
        
        // 禁用运行按钮
        const container = document.querySelector(`[data-code-id="${codeId}"]`);
        if (container) {
            const runBtn = container.querySelector('.code-block-run');
            if (runBtn) {
                runBtn.disabled = true;
                runBtn.classList.add('running');
            }
        }
    }
    
    // 处理Python执行结果
    function handlePythonResult(data) {
        const { codeId, success, output, error, executionTime, code, executionId } = data;
        const resultArea = document.getElementById(`result-${codeId}`);
        if (!resultArea) return;
        
        // 存储执行数据用于复制和引用
        resultArea.dataset.execData = JSON.stringify({
            codeId,
            executionId,
            success,
            output: output || '',
            error: error || '',
            code: code || '',
            executionTime
        });
        
        resultArea.style.display = 'block';
        resultArea.className = `code-result-area ${success ? 'success' : 'error'}`;
        
        const statusIcon = success ? '✅' : '❌';
        const statusText = success ? '执行成功' : '执行失败';
        const timeText = executionTime ? ` (${executionTime}ms)` : '';
        
        let contentHtml = '';
        
        // 显示输出
        if (output && output.trim()) {
            contentHtml += `<div class="result-output"><div class="result-output-title">输出:</div><pre>${escapeHtml(output)}</pre></div>`;
        }
        
        // 显示错误
        if (error && error.trim()) {
            contentHtml += `<div class="result-error"><div class="result-error-title">错误:</div><pre>${escapeHtml(error)}</pre></div>`;
        }
        
        // 如果没有输出也没有错误
        if (!contentHtml) {
            contentHtml = `<div class="result-empty">${success ? '执行完成，无输出' : '未知错误'}</div>`;
        }
        
        // 添加折叠、复制和@引用按钮
        resultArea.innerHTML = `<div class="result-header" onclick="toggleResultArea('${codeId}')"><span class="result-toggle">▼</span><span class="result-icon">${statusIcon}</span><span class="result-title">${statusText}${timeText}</span><div class="result-actions"><button class="result-copy" onclick="event.stopPropagation();copyExecutionResult('${codeId}')" title="复制结果">📋</button><button class="result-quote" onclick="event.stopPropagation();quoteExecutionResult('${codeId}')" title="@引用到对话">@</button><button class="result-close" onclick="event.stopPropagation();closeResultArea('${codeId}')" title="关闭">✕</button></div></div><div class="result-content">${contentHtml}</div>`;
        
        // 恢复运行按钮
        const container = document.querySelector(`[data-code-id="${codeId}"]`);
        if (container) {
            const runBtn = container.querySelector('.code-block-run');
            if (runBtn) {
                runBtn.disabled = false;
                runBtn.classList.remove('running');
            }
        }
    }

    // 按钮事件
    historyBtn.addEventListener('click', () => {
        // 切换历史面板显示状态
        historyPanel.classList.toggle('show');
        
        // 如果打开面板,请求历史会话列表
        if (historyPanel.classList.contains('show')) {
            if (window.chrome?.webview) {
                window.chrome.webview.postMessage({ type: 'getHistorySessions' });
            }
        }
    });
    
    closeHistoryBtn.addEventListener('click', () => {
        historyPanel.classList.remove('show');
    });

    newBtn.addEventListener('click', () => {
        if (window.chrome?.webview) {
            window.chrome.webview.postMessage({ type: 'newSession' });
        }
    });
    
    // 设置按钮
    settingsBtn.addEventListener('click', () => {
        if (window.chrome?.webview) {
            window.chrome.webview.postMessage({ type: 'openSettings' });
        }
    });
    
    // 点击外部关闭历史面板
    document.addEventListener('click', (e) => {
        if (historyPanel.classList.contains('show')) {
            if (!historyPanel.contains(e.target) && !historyBtn.contains(e.target)) {
                historyPanel.classList.remove('show');
            }
        }
    });
    
    // 模式切换事件
    modeToggle.addEventListener('change', () => {
        currentMode = modeToggle.checked ? 'agent' : 'chat';
        console.log('切换到模式:', currentMode);
        
        // 通知后端模式切换
        if (window.chrome?.webview) {
            window.chrome.webview.postMessage({
                type: 'modeChanged',
                mode: currentMode
            });
        }
    });
    
    // 确认对话框事件
    confirmDialogCancel.addEventListener('click', closeConfirmDialog);
    confirmDialogConfirm.addEventListener('click', confirmDelete);
    
    // 点击遮罩层关闭对话框
    confirmDialog.addEventListener('click', (e) => {
        if (e.target === confirmDialog) {
            closeConfirmDialog();
        }
    });

    // 工具调用确认对话框事件
    toolApprovalCancelBtn?.addEventListener('click', () => sendToolApprovalDecision('cancel'));
    toolApprovalAllowOnceBtn?.addEventListener('click', () => sendToolApprovalDecision('allow_once'));
    toolApprovalAllowBtn?.addEventListener('click', () => sendToolApprovalDecision('allow'));
    toolApprovalDialog?.addEventListener('click', (e) => {
        if (e.target === toolApprovalDialog) {
            sendToolApprovalDecision('cancel');
        }
    });
    
    // 图片处理函数
    function handleImageSelect(e) {
        const files = Array.from(e.target.files);
        if (files.length === 0) return;
        
        files.forEach(file => {
            if (!file.type.startsWith('image/')) {
                console.warn('跳过非图片文件:', file.name);
                return;
            }
            
            const reader = new FileReader();
            reader.onload = (event) => {
                const base64 = event.target.result;
                selectedImages.push(base64);
                updateImagePreview();
            };
            reader.readAsDataURL(file);
        });
        
        // 清空输入，允许重复选择同一文件
        e.target.value = '';
    }
    
    function handleModelChange() {
        const selectedOption = modelSelect.options[modelSelect.selectedIndex];
        if (selectedOption) {
            currentModelSupportsVision = selectedOption.dataset.supportsVision === 'true';
            
            // 显示/隐藏图片上传按钮
            if (currentModelSupportsVision) {
                imageUploadBtn.style.display = 'inline-block';
            } else {
                imageUploadBtn.style.display = 'none';
                // 如果切换到不支持视觉的模型，清空已选图片
                clearImages();
            }
        }
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
    
    function clearImages() {
        selectedImages = [];
        updateImagePreview();
    }
    
    // 全局函数，供 HTML onclick 调用
    window.removeImage = function(index) {
        selectedImages.splice(index, 1);
        updateImagePreview();
    };
});
