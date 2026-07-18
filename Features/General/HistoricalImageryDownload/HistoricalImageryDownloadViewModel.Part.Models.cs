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

        public sealed record ProviderOption(HistoricalImageryProviderType ProviderType, string DisplayName)
        {
            public override string ToString() => DisplayName;
        }


        public sealed record GoogleFallbackModeOption(GoogleNearestDateFallbackMode Mode, string DisplayName)
        {
            public override string ToString() => DisplayName;
        }


        public sealed record AreaSourceOption(HistoricalAreaSourceType AreaSourceType, string DisplayName)
        {
            public override string ToString() => DisplayName;
        }

    }
}
