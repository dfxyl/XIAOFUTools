using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#nullable enable

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Tools
{
    public sealed class PythonToolboxParameterDefinition
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public string Datatype { get; set; } = "GPString";
        public string ParameterType { get; set; } = "Optional";
        public string Direction { get; set; } = "Input";
        public object? DefaultValue { get; set; }
        public bool? MultiValue { get; set; }
        public bool? Enabled { get; set; }
        public string? Category { get; set; }
    }

    public sealed class PythonToolboxDefinition
    {
        public string? ToolboxName { get; set; }
        public string? ToolboxLabel { get; set; }
        public string? ToolClassName { get; set; }
        public string? ToolLabel { get; set; }
        public string? Description { get; set; }
        public string? ExecuteCode { get; set; }
        public List<PythonToolboxParameterDefinition> Parameters { get; set; } = new List<PythonToolboxParameterDefinition>();
    }

    public static class PythonToolboxGenerator
    {
        public static string BuildToolboxContent(PythonToolboxDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var toolboxLabel = NormalizeText(definition.ToolboxLabel, "AI Generated Tools");
            var toolClassName = SanitizePythonIdentifier(definition.ToolClassName, "AiTool");
            var toolLabel = NormalizeText(definition.ToolLabel, toolClassName);
            var description = NormalizeText(definition.Description, toolLabel);
            var executeCode = NormalizeExecuteCode(definition.ExecuteCode);
            var parameters = (definition.Parameters ?? new List<PythonToolboxParameterDefinition>())
                .Select(NormalizeParameter)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# -*- coding: utf-8 -*-");
            sb.AppendLine("\"\"\"AI generated Python toolbox for ArcGIS Pro.\"\"\"");
            sb.AppendLine("import arcpy");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("class Toolbox(object):");
            sb.AppendLine("    def __init__(self):");
            sb.AppendLine($"        self.label = {ToPythonString(toolboxLabel)}");
            sb.AppendLine($"        self.alias = {ToPythonString(SanitizeAlias(definition.ToolboxName ?? toolboxLabel))}");
            sb.AppendLine($"        self.tools = [{toolClassName}]");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine($"class {toolClassName}(object):");
            sb.AppendLine("    def __init__(self):");
            sb.AppendLine($"        self.label = {ToPythonString(toolLabel)}");
            sb.AppendLine($"        self.description = {ToPythonString(description)}");
            sb.AppendLine("        self.canRunInBackground = False");
            sb.AppendLine();
            sb.AppendLine("    def getParameterInfo(self):");
            if (parameters.Count == 0)
            {
                sb.AppendLine("        return []");
            }
            else
            {
                foreach (var parameter in parameters)
                {
                    AppendParameter(sb, parameter);
                }

                sb.AppendLine();
                sb.AppendLine("        return [");
                foreach (var parameter in parameters)
                {
                    sb.AppendLine($"            {parameter.Name},");
                }

                sb.AppendLine("        ]");
            }

            sb.AppendLine();
            sb.AppendLine("    def isLicensed(self):");
            sb.AppendLine("        return True");
            sb.AppendLine();
            sb.AppendLine("    def updateParameters(self, parameters):");
            sb.AppendLine("        return");
            sb.AppendLine();
            sb.AppendLine("    def updateMessages(self, parameters):");
            sb.AppendLine("        return");
            sb.AppendLine();
            sb.AppendLine("    def execute(self, parameters, messages):");
            sb.AppendLine("        self.params_by_name = {p.name: p for p in parameters}");
            sb.AppendLine("        def get_value(name):");
            sb.AppendLine("            param = self.params_by_name.get(name)");
            sb.AppendLine("            return None if param is None else param.valueAsText");
            sb.AppendLine("        def set_output(name, value):");
            sb.AppendLine("            param = self.params_by_name.get(name)");
            sb.AppendLine("            if param is not None:");
            sb.AppendLine("                param.value = value");
            sb.AppendLine();
            AppendIndentedCode(sb, executeCode, 8);
            sb.AppendLine();
            sb.AppendLine("        return");

            return sb.ToString();
        }

        public static string SanitizePythonIdentifier(string? value, string fallbackPrefix = "AiTool")
        {
            var fallback = string.IsNullOrWhiteSpace(fallbackPrefix) ? "AiTool" : fallbackPrefix.Trim();
            var cleaned = Regex.Replace(value ?? string.Empty, "[^a-zA-Z0-9_]", "_");
            cleaned = Regex.Replace(cleaned, "_+", "_").Trim('_');

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return fallback;
            }

            if (!char.IsLetter(cleaned[0]) && cleaned[0] != '_')
            {
                cleaned = $"{fallback}_{cleaned}";
            }

            return cleaned;
        }

        internal static string SanitizeToolboxFileName(string? value)
        {
            var cleaned = Regex.Replace(value ?? string.Empty, "[^a-zA-Z0-9_\\-]", "_");
            cleaned = Regex.Replace(cleaned, "_+", "_").Trim('_', '-');
            return string.IsNullOrWhiteSpace(cleaned) ? $"AIToolbox_{DateTime.Now:yyyyMMdd_HHmmss}" : cleaned;
        }

        private static PythonToolboxParameterDefinition NormalizeParameter(PythonToolboxParameterDefinition? input)
        {
            input ??= new PythonToolboxParameterDefinition();
            var safeName = SanitizePythonIdentifier(input.Name, "param");
            return new PythonToolboxParameterDefinition
            {
                Name = safeName,
                DisplayName = NormalizeText(input.DisplayName, safeName),
                Datatype = NormalizeText(input.Datatype, "GPString"),
                ParameterType = NormalizeChoice(input.ParameterType, "Optional", "Required", "Optional", "Derived"),
                Direction = NormalizeChoice(input.Direction, "Input", "Input", "Output"),
                DefaultValue = input.DefaultValue,
                MultiValue = input.MultiValue,
                Enabled = input.Enabled,
                Category = input.Category
            };
        }

        private static void AppendParameter(StringBuilder sb, PythonToolboxParameterDefinition parameter)
        {
            sb.AppendLine();
            sb.AppendLine($"        {parameter.Name} = arcpy.Parameter(");
            sb.AppendLine($"            displayName={ToPythonString(parameter.DisplayName)},");
            sb.AppendLine($"            name={ToPythonString(parameter.Name)},");
            sb.AppendLine($"            datatype={ToPythonString(parameter.Datatype)},");
            sb.AppendLine($"            parameterType={ToPythonString(parameter.ParameterType)},");
            sb.AppendLine($"            direction={ToPythonString(parameter.Direction)})");

            if (parameter.DefaultValue != null)
            {
                sb.AppendLine($"        {parameter.Name}.value = {ToPythonLiteral(parameter.DefaultValue)}");
            }

            if (parameter.MultiValue.HasValue)
            {
                sb.AppendLine($"        {parameter.Name}.multiValue = {ToPythonBool(parameter.MultiValue.Value)}");
            }

            if (parameter.Enabled.HasValue)
            {
                sb.AppendLine($"        {parameter.Name}.enabled = {ToPythonBool(parameter.Enabled.Value)}");
            }

            if (!string.IsNullOrWhiteSpace(parameter.Category))
            {
                sb.AppendLine($"        {parameter.Name}.category = {ToPythonString(parameter.Category)}");
            }
        }

        private static void AppendIndentedCode(StringBuilder sb, string code, int spaces)
        {
            var indent = new string(' ', spaces);
            var lines = code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var line in lines)
            {
                sb.Append(indent);
                sb.AppendLine(line);
            }
        }

        private static string NormalizeExecuteCode(string? executeCode)
        {
            return string.IsNullOrWhiteSpace(executeCode)
                ? "arcpy.AddMessage('工具已执行。')"
                : executeCode.Trim('\r', '\n');
        }

        private static string NormalizeText(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string NormalizeChoice(string? value, string fallback, params string[] allowed)
        {
            var normalized = NormalizeText(value, fallback);
            return allowed.FirstOrDefault(option => option.Equals(normalized, StringComparison.OrdinalIgnoreCase)) ?? fallback;
        }

        private static string SanitizeAlias(string? value)
        {
            var alias = Regex.Replace(value ?? string.Empty, "[^a-zA-Z0-9_]", "_");
            alias = Regex.Replace(alias, "_+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(alias) ? "ai_tools" : alias.ToLowerInvariant();
        }

        private static string ToPythonString(string? value)
        {
            return "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        }

        private static string ToPythonLiteral(object? value)
        {
            return value switch
            {
                null => "None",
                bool boolValue => ToPythonBool(boolValue),
                int or long or float or double or decimal => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "0",
                _ => ToPythonString(value.ToString() ?? string.Empty)
            };
        }

        private static string ToPythonBool(bool value)
        {
            return value ? "True" : "False";
        }
    }

}
