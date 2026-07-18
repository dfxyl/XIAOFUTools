using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.Boundary.MapBoundaryPointLineGenerator
{
    internal partial class MapBoundaryPointLineGeneratorDockPaneViewModel
    {

        /// <summary>
        /// 清理事件订阅
        /// </summary>
        public void Cleanup()
        {
            if (_mapViewInitializedToken != null)
            {
                MapViewInitializedEvent.Unsubscribe(_mapViewInitializedToken);
                _mapViewInitializedToken = null;
            }
            if (_activeMapViewChangedToken != null)
            {
                ActiveMapViewChangedEvent.Unsubscribe(_activeMapViewChangedToken);
                _activeMapViewChangedToken = null;
            }
            if (_mapSelectionChangedToken != null)
            {
                MapSelectionChangedEvent.Unsubscribe(_mapSelectionChangedToken);
                _mapSelectionChangedToken = null;
            }
        }

        private void ClearGraphicsLayerElements(GraphicsLayer graphicsLayer)
        {
            try
            {
                var elements = graphicsLayer.GetElementsAsFlattenedList();
                if (elements != null && elements.Any())
                {
                    graphicsLayer.RemoveElements(elements);
                }
            }
            catch { }
        }

    }
}
