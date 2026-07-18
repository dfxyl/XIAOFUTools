// AI 助手用户交互行为
// 由 tools/maintenance/split-ai-script.cjs 从原单文件按职责拆分。

function toggleCodeBlock(codeId) {
    const container = document.querySelector(`[data-code-id="${codeId}"]`);
    if (container) {
        container.classList.toggle('expanded');
    }
}

function closeResultArea(codeId) {
    const resultArea = document.getElementById(`result-${codeId}`);
    if (resultArea) {
        resultArea.style.display = 'none';
    }
}

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

function toggleResultArea(codeId) {
    const resultArea = document.getElementById(`result-${codeId}`);
    if (resultArea) {
        resultArea.classList.toggle('collapsed');
    }
}

function detectDangerousOperations(code) {
    const found = [];
    for (const item of DANGEROUS_PATTERNS) {
        if (item.pattern.test(code)) {
            found.push(item.desc);
        }
    }
    return found;
}

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

function executePythonCode(code, codeId) {
    console.log('[Frontend] 运行Python代码, codeId:', codeId);
    
    // 发送给C#执行
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({
            type: 'runPython',
            sessionId: currentSessionId,
            code: code,
            codeId: codeId
        });
    }
}

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
            targetAiMsg = appendMessage('ai', '正在连接模型...', true);
            turnMessageMap.set(turnId, targetAiMsg);
            if (!turnTextMap.has(turnId)) {
                turnTextMap.set(turnId, '');
            }
        }

        return targetAiMsg;
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

function clearImages() {
        selectedImages = [];
        updateImagePreview();
    }
