using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Features.User.Settings;

namespace XIAOFUTools.Features.Analysis.ViewArea
{
    internal partial class ViewAreaDockPaneViewModel
    {





        /// <summary>
        /// 格式化数值
        /// </summary>
        private string FormatValue(double value, string unit)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return "0";

            return Math.Round(value, DecimalPlaces).ToString($"F{DecimalPlaces}");
        }
    }
}
