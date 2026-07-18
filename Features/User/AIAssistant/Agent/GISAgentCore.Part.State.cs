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
using XIAOFUTools.Features.User.AIAssistant.Agent.Prompts;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tools;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tooling;
using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Services;

namespace XIAOFUTools.Features.User.AIAssistant.Agent
{
    public partial class GISAgentCore
    {

        /// <summary>
        /// 是否已完全初始化（DB + AI服务均可用）
        /// </summary>
        public bool IsFullyInitialized => _dataStore != null && _aiService != null;

        /// <summary>
        /// 获取初始化错误信息
        /// </summary>
        public string InitializationError => _initError;
    }
}
