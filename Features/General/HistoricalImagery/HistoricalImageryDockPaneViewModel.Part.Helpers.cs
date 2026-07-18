using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using XIAOFUTools.Features.General.HistoricalImageryDownload;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Features.General.HistoricalImagery
{
    internal partial class HistoricalImageryDockPaneViewModel
    {

        internal void RegisterPendingDragRequest(HistoricalImageryLayerRequest request)
        {
            _pendingDragRequest = request;
            _pendingDragRequestTimeUtc = DateTime.UtcNow;
        }


        internal void ClearPendingDragRequest()
        {
            _pendingDragRequest = null;
        }

    }
}
