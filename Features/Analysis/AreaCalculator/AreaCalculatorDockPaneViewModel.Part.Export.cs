using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.AreaCalculator
{
    internal partial class AreaCalculatorDockPaneViewModel
    {

        /// <summary>
        /// 应用右键上下文传入的图层选项
        /// </summary>
        public void ApplyContextOptions(AreaCalculatorContextOptions contextOptions)
        {
            if (contextOptions == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(contextOptions.PreferredLayerName))
            {
                PreferredLayerName = contextOptions.PreferredLayerName;
            }

            if (!string.IsNullOrWhiteSpace(contextOptions.PreferredLayerUri))
            {
                PreferredLayerUri = contextOptions.PreferredLayerUri;
            }

            TrySelectPreferredLayer();
        }

        /// <summary>
        /// 格式化面积值，根据目标字段类型返回适当的值
        /// </summary>
        private object FormatAreaValue(double area, int decimalPlaces)
        {
            if (SelectedFieldInfo == null)
                return area;

            // 根据字段类型确定如何格式化
            var fieldType = GetFieldTypeFromDisplayName(SelectedFieldInfo.FieldType);

            switch (fieldType)
            {
                case FieldType.String:
                    // 文本字段：格式化为带指定小数位数的字符串，保留尾随零
                    var formatString = decimalPlaces > 0 ? $"F{decimalPlaces}" : "F0";
                    return area.ToString(formatString);

                case FieldType.Integer:
                case FieldType.SmallInteger:
                    // 整型字段：四舍五入为整数
                    return (int)Math.Round(area);

                case FieldType.Double:
                case FieldType.Single:
                default:
                    // 数值字段：保留指定小数位数
                    return Math.Round(area, decimalPlaces);
            }
        }

        /// <summary>
        /// 根据字段类型格式化面积值（线程安全版本）
        /// </summary>
        private object FormatAreaValueByFieldType(double area, int decimalPlaces, FieldType fieldType)
        {
            switch (fieldType)
            {
                case FieldType.String:
                    // 文本字段：格式化为带指定小数位数的字符串，保留尾随零
                    var formatString = decimalPlaces > 0 ? $"F{decimalPlaces}" : "F0";
                    return area.ToString(formatString);

                case FieldType.Integer:
                case FieldType.SmallInteger:
                    // 整型字段：四舍五入为整数
                    return (int)Math.Round(area);

                case FieldType.Double:
                case FieldType.Single:
                default:
                    // 数值字段：保留指定小数位数
                    return Math.Round(area, decimalPlaces);
            }
        }
    }
}
