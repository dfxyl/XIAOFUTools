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
        /// 设置优先选中的图层名称
        /// </summary>
        public void SetPreferredLayerName(string layerName)
        {
            ApplyContextOptions(new AreaCalculatorContextOptions
            {
                PreferredLayerName = layerName
            });
        }
    }
}
