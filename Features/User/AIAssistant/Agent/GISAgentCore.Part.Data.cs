using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Features.User.AIAssistant.Agent.Infrastructure;
using XIAOFUTools.Features.User.AIAssistant.Agent.Context;
using XIAOFUTools.Features.User.AIAssistant.Agent.Prompts;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tools;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tooling;
using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Services;

namespace XIAOFUTools.Features.User.AIAssistant.Agent
{
    public partial class GISAgentCore
    {

        private int ResolveContextWindow(AIServiceConfig service)
        {
            return AgentContextBudgetPolicy.ResolveContextWindow(service);
        }


        private AIServiceConfig ResolveServiceConfig(string selectedModel)
        {
            if (_dataStore == null)
            {
                return null;
            }

            AIServiceConfig targetService = null;
            if (!string.IsNullOrWhiteSpace(selectedModel))
            {
                var allServices = _dataStore.GetAllServices();
                targetService = allServices?.FirstOrDefault(s =>
                    s.ModelName.Equals(selectedModel, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains(selectedModel, StringComparison.OrdinalIgnoreCase));
            }

            targetService ??= _dataStore.GetDefaultService();
            if (targetService == null)
            {
                var allServices = _dataStore.GetAllServices();
                targetService = allServices?.FirstOrDefault();
            }

            return targetService == null ? null : CloneServiceConfig(targetService);
        }


        /// <summary>
        /// 获取所有可用工具
        /// </summary>
        public IReadOnlyDictionary<string, IGISTool> GetAvailableTools()
        {
            return _tools;
        }

        private int GetContextInputBudget(int contextWindowTokens)
        {
            return AgentContextBudgetPolicy.GetInputBudget(contextWindowTokens);
        }


        private int GetSummarizeThreshold(int contextWindowTokens)
        {
            return AgentContextBudgetPolicy.GetSummarizeThreshold(contextWindowTokens);
        }


        private ToolExecutionRequest ResolveToolRequest(string userMessage, string selectedTool)
        {
            return ToolRequestResolver.Resolve(userMessage, selectedTool, WebFetchToolName);
        }

        private ToolExecutionSettings LoadToolExecutionSettings()
        {
            try
            {
                return _dataStore?.GetToolExecutionSettings() ?? new ToolExecutionSettings();
            }
            catch
            {
                return new ToolExecutionSettings();
            }
        }

        /// <summary>
        /// 重新加载AI服务（设置变更后调用，使新配置立即生效）
        /// </summary>
        public void ReloadAIService()
        {
            try
            {
                // 释放旧服务
                _aiService?.Dispose();
                _aiService = null;
                
                // 重新初始化
                InitializeAIService();
                
                System.Diagnostics.Debug.WriteLine($"[ReloadAIService] AI服务已重新加载, 可用: {_aiService != null}");
            }
            catch (Exception ex)
            {
                _aiService = null;
                System.Diagnostics.Debug.WriteLine($"[ReloadAIService] 重新加载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前会话ID
        /// </summary>
        public string GetCurrentSessionId()
        {
            return _currentSessionId;
        }

        /// <summary>
        /// 获取当前模式
        /// </summary>
        public string GetCurrentMode()
        {
            return _currentMode;
        }
    }
}
