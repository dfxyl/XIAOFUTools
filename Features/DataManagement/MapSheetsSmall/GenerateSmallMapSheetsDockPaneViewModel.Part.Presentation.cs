using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using XIAOFUTools.Shared;
using ArcGIS.Desktop.Framework.Dialogs;

namespace XIAOFUTools.Features.DataManagement.MapSheetsSmall
{
    internal partial class GenerateSmallMapSheetsDockPaneViewModel
    {

        private void RefreshLayers()
        {
            PolygonLayers.Clear();
            var map = MapView.Active?.Map;
            if (map == null) return;
            foreach (var fl in map.Layers.OfType<FeatureLayer>())
            {
                // 仅面图层
                if (fl.ShapeType == esriGeometryType.esriGeometryPolygon)
                    PolygonLayers.Add(fl);
            }
            if (PolygonLayers.Count > 0 && SelectedPolygonLayer == null)
                SelectedPolygonLayer = PolygonLayers[0];
        }

        private void BrowseOutputPath()
        {
            try
            {
                var initialLocation = PathDialogUtils.GetProjectDefaultGdb();
                var pickedPath = PathDialogUtils.PickSaveFeatureClassPath("选择输出位置", initialLocation);
                
                if (!string.IsNullOrEmpty(pickedPath))
                {
                    OutputFeatureClassPath = pickedPath;
                    AppendLog($"已设置输出: {OutputFeatureClassPath}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"选择输出位置出错: {ex.Message}");
            }
        }
        private void AppendLog(string msg) => LogContent += (LogContent.Length > 0 ? "\n" : string.Empty) + msg;

        private bool InsertPolygon((double xmin, double xmax, double ymin, double ymax) e, SpatialReference targetSR, FeatureClass fc, string tfh,
            string left = null, string right = null, string up = null, string low = null,
            string upl = null, string upr = null, string lowl = null, string lowr = null,
            Geometry clipInCGCS2000 = null)
        {
            // 在 CGCS2000 构建，然后投影到目标SR
            var cgcs2000 = SpatialReferenceBuilder.CreateSpatialReference(4490);
            var env = EnvelopeBuilderEx.CreateEnvelope(e.xmin, e.ymin, e.xmax, e.ymax, cgcs2000);
            var poly = PolygonBuilderEx.CreatePolygon(env);
            if (clipInCGCS2000 != null)
            {
                var inter = GeometryEngine.Instance.Intersection(poly, clipInCGCS2000);
                if (inter == null || inter.IsEmpty)
                    return false; // 仅作为“压盖筛选”，保留整幅图幅矩形
                // 不替换 poly，保持输出为标准图幅矩形
            }
            if (targetSR != null && targetSR.Wkid != cgcs2000.Wkid)
                poly = (Polygon)GeometryEngine.Instance.Project(poly, targetSR);

            using (var rb = fc.CreateRowBuffer())
            {
                rb["SHAPE"] = poly;
                rb["TFH"] = tfh;
                rb["XMin"] = e.xmin; rb["XMax"] = e.xmax; rb["YMin"] = e.ymin; rb["YMax"] = e.ymax;
                if (!string.IsNullOrEmpty(left)) rb["LeftMap"] = left;
                if (!string.IsNullOrEmpty(right)) rb["RightMap"] = right;
                if (!string.IsNullOrEmpty(up)) rb["UpMap"] = up;
                if (!string.IsNullOrEmpty(low)) rb["LowMap"] = low;
                if (!string.IsNullOrEmpty(upl)) rb["UpLMap"] = upl;
                if (!string.IsNullOrEmpty(upr)) rb["UpRMap"] = upr;
                if (!string.IsNullOrEmpty(lowl)) rb["LowLMap"] = lowl;
                if (!string.IsNullOrEmpty(lowr)) rb["LowRMap"] = lowr;
                using (var row = fc.CreateRow(rb)) { }
            }
            return true;
        }

        private static double NormalizeLon(double lon)
        {
            while (lon < -180) lon += 360;
            while (lon >= 180) lon -= 360;
            return lon;
        }
    }
}
