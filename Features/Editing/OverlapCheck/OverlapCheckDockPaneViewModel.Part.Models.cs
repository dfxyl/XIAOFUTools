using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.IO;
using System.Linq;

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.OverlapCheck
{
    internal partial class OverlapCheckDockPaneViewModel
    {

        /// <summary>
        /// 重叠信息类
        /// </summary>
        private class OverlapInfo
        {
            public Geometry Geometry { get; set; }
            public long Feature1ObjectID { get; set; }
            public long Feature2ObjectID { get; set; }
            public Dictionary<string, string> Feature1Fields { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, string> Feature2Fields { get; set; } = new Dictionary<string, string>();
        }
    }
}
