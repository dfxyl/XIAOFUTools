// AI 助手 WebView 消息与会话桥接
// 由 tools/maintenance/split-ai-script.cjs 从原单文件按职责拆分。

function isCurrentSessionEvent(data) {
        return !data.sessionId || !currentSessionId || data.sessionId === currentSessionId;
    }

function resetStreamingState() {
        isStreaming = false;
        currentStreamId = null;
        currentTurnId = null;
        currentAiMsgContent = null;
        currentStreamText = '';
        turnMessageMap.clear();
        turnTextMap.clear();
        toggleSendBtn();
    }

function markSessionStreaming(sessionId, streamId) {
        if (!sessionId || !streamId) return;
        activeSessionStreams.set(sessionId, { streamId });
    }

function clearSessionStreaming(sessionId, streamId) {
        if (!sessionId) return;
        const existing = activeSessionStreams.get(sessionId);
        if (!existing) return;
        if (!streamId || existing.streamId === streamId) {
            activeSessionStreams.delete(sessionId);
        }
    }

function syncStreamingStateFromCurrentSession() {
        const active = currentSessionId ? activeSessionStreams.get(currentSessionId) : null;
        isStreaming = !!active;
        currentStreamId = active?.streamId || null;
        toggleSendBtn();
    }

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

function sendMessage() {
        // 如果正在流式输出，则停止
        if (isStreaming) {
            console.log('[Frontend] 发送停止请求');
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({
                    type: 'stopMessage',
                    sessionId: currentSessionId,
                    streamId: currentStreamId
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
                sessionId: currentSessionId,
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

function showWelcomeMessage() {
        chatList.innerHTML = '';
        scrollToBottomBtn.style.display = 'none';
        appendMessage('ai', '你好！我是你的GIS助手，有什么可以帮你的吗？');
    }

function showStreamingError(message) {
        let updatedExisting = false;
        turnMessageMap.forEach((targetAiMsg) => {
            if (!targetAiMsg || !targetAiMsg.content) return;
            const currentText = (targetAiMsg.content.textContent || '').trim();
            if (!currentText || currentText === '正在连接模型...') {
                targetAiMsg.content.innerHTML = formatContent(`❌ ${message}`);
                updatedExisting = true;
            }
        });

        if (!updatedExisting) {
            appendMessage('ai', `❌ ${message}`);
        }
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
            option.dataset.contextWindowTokens = model.contextWindowTokens || 0;
            option.title = `${model.value} · 上下文 ${formatTokenCount(model.contextWindowTokens || 0)}${model.supportsVision ? ' · 支持图片' : ''}`;
            if (model.isDefault) {
                option.selected = true;
            }
            modelSelect.appendChild(option);
        });
        
        // 更新当前模型的视觉支持状态
        handleModelChange();
    }

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

function handleHostMessage(data) {
        if (!data || !data.type) return;

        const sessionScopedTypes = new Set([
            'streamStart',
            'assistantTurnStart',
            'assistantTurnEnd',
            'streamThinking',
            'toolCall',
            'toolApprovalRequest',
            'streamChunk',
            'streamEnd',
            'error',
            'pythonRunning',
            'pythonResult'
        ]);

        if (sessionScopedTypes.has(data.type) && !isCurrentSessionEvent(data)) {
            if (data.type === 'streamStart') {
                markSessionStreaming(data.sessionId, data.streamId);
            } else if (data.type === 'streamEnd' || data.type === 'error') {
                clearSessionStreaming(data.sessionId, data.streamId);
            }
            console.log('[Frontend] 忽略非当前会话事件:', data.type, data.sessionId, currentSessionId);
            return;
        }
        
        switch (data.type) {
            case 'modelList':
                // 收到模型列表，填充下拉框
                populateModelSelect(data.models);
                break;
            case 'sessionInfo':
                // 收到当前会话信息
                currentSessionId = data.sessionId;
                if (data.isNewSession) {
                    resetStreamingState();
                    showWelcomeMessage();
                } else {
                    syncStreamingStateFromCurrentSession();
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
                resetStreamingState();
                syncStreamingStateFromCurrentSession();
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
                markSessionStreaming(data.sessionId, data.streamId);
                isStreaming = true;
                currentStreamId = data.streamId || null;
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
                pendingToolApprovalRequestId = null;
                toolApprovalDialog?.classList.remove('show');
                turnTextMap.forEach((_, turnId) => finalizeTurnMessage(turnId));
                clearSessionStreaming(data.sessionId, data.streamId);
                resetStreamingState();
                toggleSendBtn();
                // 更新滚动按钮状态
                updateScrollButton();
                }
                break;
            case 'error':
                showStreamingError(data.message);
                pendingToolApprovalRequestId = null;
                toolApprovalDialog?.classList.remove('show');
                clearSessionStreaming(data.sessionId, data.streamId);
                resetStreamingState();
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
