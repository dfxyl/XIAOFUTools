#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload
{
    internal sealed partial class HistoricalImageryDownloadViewModel
    {

        private async void OnExtentCreated(Envelope extent)
        {
            _customExtent = extent;
            UpdateAreaSummary();
            UpdateCommands();

            try
            {
                await SketchToolResetWorkflow.ResetAsync(ArcGisSketchToolResetOperations.Instance);
            }
            catch (Exception ex)
            {
                AppendLog($"恢复地图工具状态失败: {ex.Message}");
            }
        }


        private void ApplyVersions(IEnumerable<HistoricalVersionItem> versions)
        {
            ResetVersions();

            foreach (var version in versions)
            {
                var selection = new HistoricalVersionSelectionItem(version);
                selection.PropertyChanged += OnVersionSelectionChanged;
                Versions.Add(selection);
            }

            SelectedVersionPreview = Versions.FirstOrDefault();
            _hasVersionResults = Versions.Count > 0;
            UpdateSelectedVersionSummary();
            UpdateCommands();
            OnPropertyChanged(nameof(CanConfigureVersions));
        }

    }
}
