using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ExcelDataReader;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.ShapefileBuilder
{
    public partial class ShapefileBuilderViewModel
    {

        /// <summary>
        /// 初始化
        /// </summary>
        private void Initialize()
        {
            _inputExcelPath = string.Empty;
            _outputFolderPath = string.Empty;
            _isProcessing = false;
            _logText = string.Empty;
            _selectedSpatialReference = SpatialReferences.WGS84;
            _selectedCoordinateSystemName = $"{_selectedSpatialReference.Name} (WKID: {_selectedSpatialReference.Wkid})";
        }

        /// <summary>
        /// 初始化命令
        /// </summary>
        private void InitializeCommands()
        {
            BrowseInputExcelCommand = new RelayCommand(() => BrowseInputExcel(), () => !IsProcessing);
            BrowseOutputFolderCommand = new RelayCommand(() => BrowseOutputFolder(), () => !IsProcessing);
            ExportTemplateCommand = new RelayCommand(() => ExportTemplate(), () => !IsProcessing);
            SelectCoordinateSystemCommand = new RelayCommand(() => SelectCoordinateSystem(), () => !IsProcessing);
            StartCommand = new RelayCommand(() => StartBuildShapefile(), () => CanStart());
            StopCommand = new RelayCommand(() => StopBuildShapefile(), () => IsProcessing);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
        }

        /// <summary>
        /// 规范化SHP要素类名称
        /// </summary>
        private string NormalizeShapefileDatasetName(string layerName, int index)
        {
            string name = (layerName ?? string.Empty).Trim();
            if (name.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidChar, '_');
            }

            name = name.Replace(' ', '_');

            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Layer_{Math.Max(1, index)}";
            }

            if (char.IsDigit(name[0]))
            {
                name = "L" + name;
            }

            if (name.Length > 64)
            {
                name = name.Substring(0, 64);
            }

            return name;
        }

        /// <summary>
        /// 规范化SHP字段名（长度限制10，并保证唯一）
        /// </summary>
        private string NormalizeShapefileFieldName(string fieldName, HashSet<string> usedFieldNames)
        {
            string name = (fieldName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "FIELD";
            }

            name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "FIELD";
            }

            if (char.IsDigit(name[0]))
            {
                name = "F" + name;
            }

            if (name.Length > 10)
            {
                name = name.Substring(0, 10);
            }

            string candidate = name;
            int suffix = 1;
            while (usedFieldNames.Contains(candidate))
            {
                string suffixText = suffix.ToString();
                int baseLength = Math.Max(1, 10 - suffixText.Length);
                string baseName = name.Length > baseLength ? name.Substring(0, baseLength) : name;
                candidate = baseName + suffixText;
                suffix++;
            }

            usedFieldNames.Add(candidate);
            return candidate;
        }

        /// <summary>
        /// 规范化文本字段长度
        /// </summary>
        private int NormalizeTextLength(int? length)
        {
            int value = length.GetValueOrDefault(254);
            if (value <= 0)
            {
                value = 254;
            }

            if (value > 254)
            {
                value = 254;
            }

            return value;
        }
    }
}
