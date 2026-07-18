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
using XIAOFUTools.Features.General.InternetTileDownload.Infrastructure;
using XIAOFUTools.Features.General.InternetTileDownload.Services;

namespace XIAOFUTools.Features.General.InternetTileDownload
{
    internal sealed partial class InternetTileDownloadViewModel
    {

        private void ApplyResolvedService(InternetTileServiceDefinition definition)
        {
            LevelOptions.Clear();
            foreach (var level in definition.Levels)
            {
                LevelOptions.Add(new LevelOption(level.LevelId, $"级别 {level.LevelId}"));
            }

            SelectedLevelOption = GetPreferredLevelOption(definition);
            ServiceSummary = InternetTileServiceSummaryBuilder.Build(definition);
            UpdateCommands();
        }


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

    }
}
