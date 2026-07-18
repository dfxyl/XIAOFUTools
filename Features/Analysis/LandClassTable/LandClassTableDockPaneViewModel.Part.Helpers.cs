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

        private void ManageReportProfiles()
        {
            var current = SelectedReportProfile;
            if (_reportProfileDialogService.Show(ReportProfiles, current, out var updatedProfile))
            {
                NotifyPropertyChanged(() => ReportProfiles);
                SelectedReportProfile = updatedProfile ?? ReportProfiles.FirstOrDefault();
                if (SelectedReportProfile != null)
                {
                    ApplyReportProfile(SelectedReportProfile);
                }
            }
        }


        private static void AddUnmatchedLandClass(
            Dictionary<string, UnmatchedLandClassInfo> unmatchedClasses,
            string landClassName,
            string projectName,
            string plotName)
        {
            string displayValue = string.IsNullOrWhiteSpace(landClassName)
                ? "为空"
                : $"“{landClassName.Trim()}”";
            if (!unmatchedClasses.TryGetValue(displayValue, out var info))
            {
                info = new UnmatchedLandClassInfo(displayValue);
                unmatchedClasses[displayValue] = info;
            }

            info.Count++;
            string location = string.IsNullOrWhiteSpace(plotName)
                ? projectName
                : $"{projectName}/{plotName}";
            if (!string.IsNullOrWhiteSpace(location) &&
                !info.Locations.Contains(location, StringComparer.OrdinalIgnoreCase))
            {
                info.Locations.Add(location);
            }
        }


        private static string NormalizeGroupValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未命名" : value.Trim();
        }



        private static string NormalizeOutputPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "未命名";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Trim().Where(ch => !invalid.Contains(ch)).ToArray();
            var text = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(text) ? "未命名" : text;
        }



        private static double RoundArea(double value, int decimalPlaces)
        {
            if (Math.Abs(value) < Math.Pow(10, -decimalPlaces) / 2)
            {
                return 0;
            }

            return Math.Round(value, decimalPlaces);
        }


        private static bool IsChildColumn(IReadOnlyList<LandClassTableColumn> columns, LandClassTableColumn parent, LandClassTableColumn child)
        {
            if (parent.Level == LandClassTableColumnLevel.TopGroup)
            {
                return child.TopGroupKey.Equals(parent.Key, StringComparison.OrdinalIgnoreCase) &&
                       (child.Level == LandClassTableColumnLevel.SecondGroup ||
                        child.Level == LandClassTableColumnLevel.Leaf && !columns.Any(x =>
                            x.Level == LandClassTableColumnLevel.SecondGroup &&
                            x.Key.Equals(child.SecondGroupKey, StringComparison.OrdinalIgnoreCase)));
            }

            if (parent.Level == LandClassTableColumnLevel.SecondGroup)
            {
                return child.Level == LandClassTableColumnLevel.Leaf &&
                       child.SecondGroupKey.Equals(parent.Key, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }


        private static bool HasLeafChildren(IReadOnlyList<LandClassTableColumn> columns, string secondGroupKey)
        {
            return columns.Any(x =>
                x.Level == LandClassTableColumnLevel.Leaf &&
                x.SecondGroupKey.Equals(secondGroupKey, StringComparison.OrdinalIgnoreCase));
        }



        private static bool IsSystemField(FieldType fieldType)
        {
            return fieldType == FieldType.Geometry ||
                   fieldType == FieldType.OID ||
                   fieldType == FieldType.GlobalID ||
                   fieldType == FieldType.Blob ||
                   fieldType == FieldType.Raster;
        }


        private static bool IsNumericField(FieldType fieldType)
        {
            return fieldType == FieldType.Double ||
                   fieldType == FieldType.Single ||
                   fieldType == FieldType.Integer ||
                   fieldType == FieldType.SmallInteger ||
                   fieldType == FieldType.BigInteger;
        }

    }
}
