using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Features.User.AIAssistant.Agent
{
    /// <summary>
    /// GIS工具接口
    /// </summary>
    public interface IGISTool
    {
        /// <summary>
        /// 工具名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 工具描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 工具参数定义(JSON Schema)
        /// </summary>
        JObject ParametersSchema { get; }

        /// <summary>
        /// 执行工具
        /// </summary>
        Task<ToolResult> ExecuteAsync(JObject parameters);
    }

    /// <summary>
    /// 工具执行结果
    /// </summary>
    public class ToolResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }

        public static ToolResult CreateSuccess(string message, object data = null)
        {
            return new ToolResult
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ToolResult CreateError(string error)
        {
            return new ToolResult
            {
                Success = false,
                Error = error
            };
        }
    }
}
