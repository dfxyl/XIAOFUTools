using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Tools
{
    public sealed class CreatePythonToolboxTool : IGISTool
    {
        public const string ToolName = "create_python_toolbox";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public string Name => ToolName;

        public string Description => "在当前 ArcGIS Pro 工程所在目录生成 .pyt Python 工具箱，并自动添加到当前工程。用于把可复用的地理处理逻辑沉淀为工程工具箱。";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["required"] = new JArray("toolbox_name", "tool_class_name", "tool_label", "execute_code"),
            ["properties"] = new JObject
            {
                ["toolbox_name"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "工具箱文件名，不需要 .pyt 后缀，只允许生成在当前工程目录的 AI_Toolboxes 子目录。"
                },
                ["toolbox_label"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "工具箱在 ArcGIS Pro 中显示的名称。"
                },
                ["tool_class_name"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Python 工具类名，必须表达工具用途。非法字符会自动转为合法标识符。"
                },
                ["tool_label"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "工具在 ArcGIS Pro 中显示的名称。"
                },
                ["description"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "工具说明。"
                },
                ["execute_code"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "写入 execute(self, parameters, messages) 内部的 Python 代码。可使用 arcpy、parameters、messages、self.params_by_name、get_value(name)、set_output(name, value)。"
                },
                ["parameters"] = new JObject
                {
                    ["type"] = "array",
                    ["description"] = "ArcPy 参数定义数组。",
                    ["items"] = new JObject
                    {
                        ["type"] = "object",
                        ["required"] = new JArray("name", "display_name", "datatype"),
                        ["properties"] = new JObject
                        {
                            ["name"] = new JObject { ["type"] = "string" },
                            ["display_name"] = new JObject { ["type"] = "string" },
                            ["datatype"] = new JObject { ["type"] = "string", ["description"] = "例如 GPString、DEFeatureClass、GPFeatureLayer、DEFolder、DEFile、GPLong、GPDouble、GPBoolean。" },
                            ["parameter_type"] = new JObject { ["type"] = "string", ["enum"] = new JArray("Required", "Optional", "Derived") },
                            ["direction"] = new JObject { ["type"] = "string", ["enum"] = new JArray("Input", "Output") },
                            ["default_value"] = new JObject { ["description"] = "可选默认值。" },
                            ["multi_value"] = new JObject { ["type"] = "boolean" },
                            ["enabled"] = new JObject { ["type"] = "boolean" },
                            ["category"] = new JObject { ["type"] = "string" }
                        }
                    }
                },
                ["overwrite"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = true,
                    ["description"] = "同名 .pyt 已存在时是否覆盖。"
                }
            }
        };

        public async Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            try
            {
                var definition = BuildDefinition(parameters);
                var overwrite = parameters?["overwrite"]?.ToObject<bool?>() ?? true;
                var result = await WriteAndAddToProjectAsync(definition, overwrite);
                return ToolResult.CreateSuccess(
                    $"已生成 Python 工具箱: {result.ToolboxPath}。{result.AddToProjectMessage}",
                    new
                    {
                        toolboxPath = result.ToolboxPath,
                        toolboxFolder = result.ToolboxFolder,
                        toolboxName = result.ToolboxName,
                        toolClassName = result.ToolClassName,
                        addedToProject = result.AddedToProject,
                        addToProjectMessage = result.AddToProjectMessage
                    });
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError($"创建 Python 工具箱失败: {ex.Message}");
            }
        }

        private static async Task<PythonToolboxInstallResult> WriteAndAddToProjectAsync(PythonToolboxDefinition definition, bool overwrite)
        {
            var content = PythonToolboxGenerator.BuildToolboxContent(definition);
            var projectFolder = GetCurrentProjectFolder();
            var toolboxName = PythonToolboxGenerator.SanitizeToolboxFileName(definition.ToolboxName ?? definition.ToolboxLabel ?? definition.ToolClassName);
            var toolboxFolder = Path.Combine(projectFolder, "AI_Toolboxes");
            var toolboxPath = Path.Combine(toolboxFolder, $"{toolboxName}.pyt");

            Directory.CreateDirectory(toolboxFolder);
            if (File.Exists(toolboxPath) && !overwrite)
            {
                throw new InvalidOperationException($"工具箱已存在: {toolboxPath}");
            }

            File.WriteAllText(toolboxPath, content, Utf8NoBom);

            var addMessage = "已写入工具箱文件，未添加到工程。";
            var added = false;
            try
            {
                added = await AddToolboxToProjectAsync(toolboxPath);
                addMessage = added ? "已添加到当前工程。" : "工具箱可能已在当前工程中。";
            }
            catch (Exception ex)
            {
                addMessage = $"工具箱文件已创建，但添加到工程失败: {ex.Message}";
            }

            return new PythonToolboxInstallResult
            {
                ToolboxPath = toolboxPath,
                ToolboxFolder = toolboxFolder,
                ToolboxName = toolboxName,
                ToolClassName = PythonToolboxGenerator.SanitizePythonIdentifier(definition.ToolClassName, "AiTool"),
                AddedToProject = added,
                AddToProjectMessage = addMessage
            };
        }

        private static string GetCurrentProjectFolder()
        {
            var project = Project.Current;
            if (project == null)
            {
                throw new InvalidOperationException("当前未打开 ArcGIS Pro 工程。");
            }

            if (!string.IsNullOrWhiteSpace(project.HomeFolderPath))
            {
                return project.HomeFolderPath;
            }

            if (!string.IsNullOrWhiteSpace(project.URI))
            {
                var path = project.URI;
                if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile)
                {
                    path = uri.LocalPath;
                }

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    return dir;
                }
            }

            if (!string.IsNullOrWhiteSpace(project.DefaultToolboxPath))
            {
                var dir = Path.GetDirectoryName(project.DefaultToolboxPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    return dir;
                }
            }

            throw new InvalidOperationException("无法解析当前工程所在目录，请先保存 ArcGIS Pro 工程。");
        }

        private static Task<bool> AddToolboxToProjectAsync(string toolboxPath)
        {
            return QueuedTask.Run(() =>
            {
                var project = Project.Current;
                if (project == null)
                {
                    throw new InvalidOperationException("当前未打开 ArcGIS Pro 工程。");
                }

                var item = ItemFactory.Instance.Create(toolboxPath);
                if (item is IProjectItem projectItem)
                {
                    return project.AddItem(projectItem);
                }

                throw new InvalidOperationException("无法从 .pyt 文件创建 ArcGIS Pro 工程项。");
            });
        }

        private static PythonToolboxDefinition BuildDefinition(JObject parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentException("参数不能为空。");
            }

            var definition = new PythonToolboxDefinition
            {
                ToolboxName = parameters["toolbox_name"]?.ToString(),
                ToolboxLabel = parameters["toolbox_label"]?.ToString(),
                ToolClassName = parameters["tool_class_name"]?.ToString(),
                ToolLabel = parameters["tool_label"]?.ToString(),
                Description = parameters["description"]?.ToString(),
                ExecuteCode = parameters["execute_code"]?.ToString()
            };

            if (string.IsNullOrWhiteSpace(definition.ToolboxName))
            {
                throw new ArgumentException("toolbox_name 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(definition.ToolClassName))
            {
                throw new ArgumentException("tool_class_name 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(definition.ToolLabel))
            {
                throw new ArgumentException("tool_label 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(definition.ExecuteCode))
            {
                throw new ArgumentException("execute_code 不能为空。");
            }

            var parameterArray = parameters["parameters"] as JArray;
            if (parameterArray != null)
            {
                foreach (var item in parameterArray.OfType<JObject>())
                {
                    definition.Parameters.Add(new PythonToolboxParameterDefinition
                    {
                        Name = item["name"]?.ToString(),
                        DisplayName = item["display_name"]?.ToString(),
                        Datatype = item["datatype"]?.ToString() ?? "GPString",
                        ParameterType = item["parameter_type"]?.ToString() ?? "Optional",
                        Direction = item["direction"]?.ToString() ?? "Input",
                        DefaultValue = item["default_value"] is JValue value ? value.Value : item["default_value"]?.ToString(),
                        MultiValue = item["multi_value"]?.ToObject<bool?>(),
                        Enabled = item["enabled"]?.ToObject<bool?>(),
                        Category = item["category"]?.ToString()
                    });
                }
            }

            return definition;
        }

        private sealed class PythonToolboxInstallResult
        {
            public string ToolboxPath { get; set; }
            public string ToolboxFolder { get; set; }
            public string ToolboxName { get; set; }
            public string ToolClassName { get; set; }
            public bool AddedToProject { get; set; }
            public string AddToProjectMessage { get; set; }
        }
    }
}
