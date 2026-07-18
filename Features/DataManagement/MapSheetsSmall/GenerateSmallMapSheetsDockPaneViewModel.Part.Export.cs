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
using XIAOFUTools.Shared.Presentation;
using ArcGIS.Desktop.Framework.Dialogs;

namespace XIAOFUTools.Features.DataManagement.MapSheetsSmall
{
    internal partial class GenerateSmallMapSheetsDockPaneViewModel
    {

        private async void OnExtentCreated(Envelope env)
        {
            try
            {
                _drawnExtent = env;
                NotifyPropertyChanged(() => DrawnExtentText);
                NotifyPropertyChanged(() => CanRun);
                await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }
        private async Task<OutputFeatureClassLease> CreateOutputFeatureClassAsync(string path, SpatialReference targetSR)
        {
            // 使用OutputDatasetUtils自动识别GDB或SHP
            var info = OutputDatasetUtils.ParseOutputPath(path, "MapSheets");
            
            // 检查是否存在,并提示覆盖
            if (OutputDatasetUtils.Exists(info))
            {
                bool overwrite = false;
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    var msg = $"输出要素{(info.IsGdb ? "类" : "")}已存在:{info.CatalogPath}。是否覆盖?";
                    var result = PresentationServices.Dialogs.Show(msg, "覆盖确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                    overwrite = result == System.Windows.MessageBoxResult.Yes;
                });
                if (!overwrite)
                    throw new OperationCanceledException("用户取消覆盖,操作已中止。");

                var deleteParams = Geoprocessing.MakeValueArray(info.CatalogPath);
                await Geoprocessing.ExecuteToolAsync(
                    "Delete_management",
                    deleteParams,
                    null,
                    CancellationToken.None,
                    null,
                    GPExecuteToolFlags.GPThread);
            }

            // 创建面要素类(自动支持GDB和SHP)
            var outName = info.IsGdb ? info.OutNameNoExt : info.OutNameNoExt + ".shp";
            var createParams = Geoprocessing.MakeValueArray(info.OutPathWorkspace, outName, "POLYGON", "#", "DISABLED", "DISABLED", targetSR);
            var r = await Geoprocessing.ExecuteToolAsync(
                "CreateFeatureclass_management",
                createParams,
                null,
                CancellationToken.None,
                null,
                GPExecuteToolFlags.GPThread);
            if (r == null || r.IsFailed)
                throw new InvalidOperationException("创建要素类失败");

            // 追加字段
            string[] textFields = { "TFH","LeftMap","RightMap","UpMap","LowMap","UpLMap","UpRMap","LowLMap","LowRMap" };
            foreach (var f in textFields)
            {
                var addParams = Geoprocessing.MakeValueArray(info.CatalogPath, f, "TEXT", "#", "#", 64);
                var rr = await Geoprocessing.ExecuteToolAsync(
                    "AddField_management",
                    addParams,
                    null,
                    CancellationToken.None,
                    null,
                    GPExecuteToolFlags.GPThread);
                if (rr == null || rr.IsFailed) throw new InvalidOperationException($"添加字段失败: {f}");
            }
            string[] dblFields = { "XMin","XMax","YMin","YMax" };
            foreach (var f in dblFields)
            {
                var addParams = Geoprocessing.MakeValueArray(info.CatalogPath, f, "DOUBLE");
                var rr = await Geoprocessing.ExecuteToolAsync(
                    "AddField_management",
                    addParams,
                    null,
                    CancellationToken.None,
                    null,
                    GPExecuteToolFlags.GPThread);
                if (rr == null || rr.IsFailed) throw new InvalidOperationException($"添加字段失败: {f}");
            }

            // 打开要素类(GDB和SHP使用不同方式)
            if (info.IsGdb)
            {
                var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(info.GdbRoot)));
                return new OutputFeatureClassLease(gdb.OpenDataset<FeatureClass>(info.RelativePathInGdb), gdb);
            }
            else
            {
                var conn = new FileSystemConnectionPath(new Uri(info.OutPathWorkspace), FileSystemDatastoreType.Shapefile);
                var fsds = new FileSystemDatastore(conn);
                return new OutputFeatureClassLease(fsds.OpenDataset<FeatureClass>(info.OutNameNoExt), fsds);
            }
        }

        private sealed class OutputFeatureClassLease : System.IDisposable
        {
            private readonly System.IDisposable _workspace;

            public OutputFeatureClassLease(FeatureClass featureClass, System.IDisposable workspace)
            {
                FeatureClass = featureClass;
                _workspace = workspace;
            }

            public FeatureClass FeatureClass { get; }

            public void Dispose()
            {
                FeatureClass.Dispose();
                _workspace.Dispose();
            }
        }

        // 计算选中图层在 CGCS2000 下的联合多边形（仅限与范围相交部分）
        private Geometry BuildLayerUnionInCGCS2000(FeatureLayer fl, SpatialReference cgcs2000, Envelope sourceExtent, SpatialReference sourceExtentSr)
        {
            if (fl == null) return null;
            var fc = fl.GetFeatureClass();
            var layerSR = fl.GetSpatialReference();
            var geoms = new System.Collections.Generic.List<Geometry>();
            QueryFilter filter = null;
            if (sourceExtent != null)
            {
                var extentInLayer =
                    sourceExtentSr != null && layerSR != null && sourceExtentSr.Wkid == layerSR.Wkid
                        ? sourceExtent
                        : (Envelope)GeometryEngine.Instance.Project(sourceExtent, layerSR);

                filter = new SpatialQueryFilter
                {
                    FilterGeometry = extentInLayer,
                    SpatialRelationship = SpatialRelationship.Intersects
                };
            }

            using (var cursor = fc.Search(filter, false))
            {
                while (cursor.MoveNext())
                {
                    using (var row = cursor.Current as Feature)
                    {
                        var g = row.GetShape();
                        if (g == null || g.IsEmpty) continue;
                        if (g.SpatialReference == null || g.SpatialReference.Wkid != layerSR.Wkid)
                            g = GeometryEngine.Instance.Project(g, layerSR);
                        var g2 = GeometryEngine.Instance.Project(g, cgcs2000);
                        geoms.Add(g2);
                    }
                }
            }
            if (geoms.Count == 0) return null;
            return GeometryEngine.Instance.Union(geoms);
        }
    }
}
