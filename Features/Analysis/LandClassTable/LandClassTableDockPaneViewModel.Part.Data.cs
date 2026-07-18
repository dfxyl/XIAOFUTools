using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    internal sealed partial class LandClassTableDockPaneViewModel
    {

        private void LoadRedlineFields()
        {
            RedlineFields.Clear();
            RedlineOptionalFields.Clear();
            RedlineNumericFields.Clear();
            SelectedAreaField = null;
            SelectedProjectNameField = null;
            SelectedGroupField = null;
            SelectedPlotNameField = null;

            if (SelectedRedlineLayer == null)
            {
                return;
            }

            Task.Run(async () =>
            {
                var allFields = new List<LandClassFieldItem>();
                var numericFields = new List<LandClassFieldItem>();
                await QueuedTask.Run(() =>
                {
                    var definition = SelectedRedlineLayer.GetFeatureClass()?.GetDefinition();
                    if (definition == null)
                    {
                        return;
                    }

                    foreach (var field in definition.GetFields().Where(x => !IsSystemField(x.FieldType)))
                    {
                        var item = CreateFieldItem(field);
                        allFields.Add(item);
                        if (IsNumericField(field.FieldType))
                        {
                            numericFields.Add(item);
                        }
                    }
                });

                PresentationServices.UiThread.Invoke(() =>
                {
                    RedlineFields.Add(new LandClassFieldItem
                    {
                        IsEmptyOption = true,
                        Alias = "使用红线图层名"
                    });
                    RedlineOptionalFields.Add(new LandClassFieldItem
                    {
                        IsEmptyOption = true,
                        Alias = "不选择"
                    });
                    RedlineNumericFields.Add(new LandClassFieldItem
                    {
                        IsEmptyOption = true,
                        Alias = "自动计算几何面积"
                    });
                    foreach (var field in allFields)
                    {
                        RedlineFields.Add(field);
                        RedlineOptionalFields.Add(field);
                    }

                    foreach (var field in numericFields)
                    {
                        RedlineNumericFields.Add(field);
                    }

                    SelectedProjectNameField = RedlineFields.FirstOrDefault();
                    SelectedGroupField = RedlineOptionalFields.FirstOrDefault(x => x.IsEmptyOption);
                    SelectedPlotNameField = RedlineOptionalFields.FirstOrDefault(x => x.IsEmptyOption);
                    SelectedAreaField = RedlineNumericFields.FirstOrDefault();
                    NotifyPropertyChanged(() => RedlineOptionalFields);
                    NotifyCanProcessChanged();
                });
            });
        }


        private void LoadClassFields()
        {
            ClassFields.Clear();
            SelectedClassNameField = null;
            SelectedOwnerUnitField = null;
            SelectedOwnerNatureField = null;
            if (SelectedClassLayer == null)
            {
                return;
            }

            Task.Run(async () =>
            {
                var fields = new List<LandClassFieldItem>();
                await QueuedTask.Run(() =>
                {
                    var definition = SelectedClassLayer.GetFeatureClass()?.GetDefinition();
                    if (definition == null)
                    {
                        return;
                    }

                    foreach (var field in definition.GetFields().Where(x => !IsSystemField(x.FieldType)))
                    {
                        fields.Add(CreateFieldItem(field));
                    }
                });

                PresentationServices.UiThread.Invoke(() =>
                {
                    foreach (var field in fields)
                    {
                        ClassFields.Add(field);
                    }

                    SelectedClassNameField = ClassFields.FirstOrDefault(x =>
                        x.FieldName.Equals("DLMC", StringComparison.OrdinalIgnoreCase) ||
                        x.Alias.IndexOf("地类名称", StringComparison.OrdinalIgnoreCase) >= 0) ??
                        ClassFields.FirstOrDefault(x =>
                            x.FieldName.Equals("DLBM", StringComparison.OrdinalIgnoreCase) ||
                            x.FieldName.IndexOf("DLBM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            x.Alias.IndexOf("地类编码", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            x.Alias.IndexOf("地类代码", StringComparison.OrdinalIgnoreCase) >= 0) ??
                        ClassFields.FirstOrDefault(x =>
                            x.Alias.IndexOf("地类", StringComparison.OrdinalIgnoreCase) >= 0) ??
                        ClassFields.FirstOrDefault();
                    SelectedOwnerUnitField = ClassFields.FirstOrDefault(x =>
                        x.FieldName.Equals("QSDWMC", StringComparison.OrdinalIgnoreCase) ||
                        x.Alias.IndexOf("权属单位", StringComparison.OrdinalIgnoreCase) >= 0);
                    SelectedOwnerNatureField = ClassFields.FirstOrDefault(x =>
                        x.FieldName.Equals("QSXZ", StringComparison.OrdinalIgnoreCase) ||
                        x.Alias.IndexOf("权属性质", StringComparison.OrdinalIgnoreCase) >= 0);
                    NotifyCanProcessChanged();
                });
            });
        }


        private string ReadProjectName(Feature feature)
        {
            if (SelectedProjectNameField != null && !SelectedProjectNameField.IsEmptyOption)
            {
                string value = Convert.ToString(feature[SelectedProjectNameField.FieldName], CultureInfo.CurrentCulture);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return SelectedRedlineLayer?.Name?.Trim() ?? string.Empty;
        }


        private double ReadTotalArea(Feature feature, Polygon polygon)
        {
            if (SelectedAreaField != null && !SelectedAreaField.IsEmptyOption)
            {
                double area = ConvertToDouble(feature[SelectedAreaField.FieldName]);
                if (area > 0)
                {
                    return area;
                }
            }

            return ConvertAreaUnit(CalculateSquareMeterArea(polygon), SelectedAreaUnit);
        }


        private static string ReadOptionalField(Feature feature, LandClassFieldItem field)
        {
            if (feature == null || field == null || field.IsEmptyOption || string.IsNullOrWhiteSpace(field.FieldName))
            {
                return string.Empty;
            }

            string value = Convert.ToString(feature[field.FieldName], CultureInfo.CurrentCulture);
            return value?.Trim() ?? string.Empty;
        }


        private string ReadOwnerUnit(Feature feature)
        {
            if (!UseAutoOwnerMapping)
            {
                return ManualOwnerUnitName?.Trim() ?? string.Empty;
            }

            return ReadOptionalField(feature, SelectedOwnerUnitField);
        }


        private string ReadOwnerNature(Feature feature)
        {
            if (!UseAutoOwnerMapping)
            {
                return ManualOwnerNatureName?.Trim() ?? string.Empty;
            }

            return ReadOptionalField(feature, SelectedOwnerNatureField);
        }


        private static double ConvertToDouble(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            if (value is IConvertible)
            {
                try
                {
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return 0;
                }
            }

            return 0;
        }


        private static double CalculateSquareMeterArea(Polygon polygon)
        {
            if (polygon == null || polygon.IsEmpty)
            {
                return 0;
            }

            if (polygon.SpatialReference != null && polygon.SpatialReference.IsGeographic)
            {
                return Math.Abs(GeometryEngine.Instance.GeodesicArea(polygon));
            }

            return Math.Abs(polygon.Area);
        }


        private static double ConvertAreaUnit(double squareMeters, string targetUnit)
        {
            return targetUnit switch
            {
                "自动" => squareMeters,
                "公顷" => squareMeters / 10000.0,
                "亩" => squareMeters / 666.6666666667,
                _ => squareMeters
            };
        }


        private static string GetColumnName(int columnNumber)
        {
            int dividend = columnNumber;
            string columnName = string.Empty;
            while (dividend > 0)
            {
                int modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }

            return columnName;
        }


        private static string GetFieldTypeDisplayName(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.Double => "双精度",
                FieldType.Single => "单精度",
                FieldType.Integer => "整型",
                FieldType.SmallInteger => "短整型",
                FieldType.BigInteger => "长整型",
                FieldType.String => "文本",
                FieldType.Date => "日期",
                _ => "其他"
            };
        }

    }
}
