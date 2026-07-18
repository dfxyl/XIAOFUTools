// AI 助手启动与事件装配
// 由 tools/maintenance/split-ai-script.cjs 从原单文件按职责拆分。

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

inputText.addEventListener('input', function() {
        this.style.height = 'auto';
        this.style.height = Math.min(this.scrollHeight, 150) + 'px';
        toggleSendBtn();
    });

toggleSendBtn();

inputText.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

sendBtn.addEventListener('click', sendMessage);

imageUploadBtn.addEventListener('click', () => {
        imageInput.click();
    });

imageInput.addEventListener('change', handleImageSelect);

modelSelect.addEventListener('change', handleModelChange);

chatList.addEventListener('scroll', () => {
        // 防抖，避免频繁更新
        clearTimeout(scrollTimeout);
        scrollTimeout = setTimeout(() => {
            updateScrollButton();
        }, 100);
    });

scrollToBottomBtn.addEventListener('click', forceScrollToBottom);

window.__xiaofuHandleHostMessage = handleHostMessage;

window.addEventListener('message', (event) => handleHostMessage(event.data));

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

settingsBtn.addEventListener('click', () => {
        if (window.chrome?.webview) {
            window.chrome.webview.postMessage({ type: 'openSettings' });
        }
    });

document.addEventListener('click', (e) => {
        if (historyPanel.classList.contains('show')) {
            if (!historyPanel.contains(e.target) && !historyBtn.contains(e.target)) {
                historyPanel.classList.remove('show');
            }
        }
    });

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

confirmDialogCancel.addEventListener('click', closeConfirmDialog);

confirmDialogConfirm.addEventListener('click', confirmDelete);

confirmDialog.addEventListener('click', (e) => {
        if (e.target === confirmDialog) {
            closeConfirmDialog();
        }
    });

toolApprovalCancelBtn?.addEventListener('click', () => sendToolApprovalDecision('cancel'));

toolApprovalAllowOnceBtn?.addEventListener('click', () => sendToolApprovalDecision('allow_once'));

toolApprovalAllowBtn?.addEventListener('click', () => sendToolApprovalDecision('allow'));

toolApprovalDialog?.addEventListener('click', (e) => {
        if (e.target === toolApprovalDialog) {
            sendToolApprovalDecision('cancel');
        }
    });

window.removeImage = function(index) {
        selectedImages.splice(index, 1);
        updateImagePreview();
    };
