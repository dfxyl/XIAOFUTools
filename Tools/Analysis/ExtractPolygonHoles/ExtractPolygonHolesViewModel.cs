using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.ExtractPolygonHoles
{
    internal class ExtractPolygonHolesViewModel : PropertyChangedBase
    {
        private ObservableCollection<FeatureLayer> _polygonLayers = new();
        private FeatureLayer _selectedPolygonLayer;
        private string _outputPath = string.Empty;
        private bool _createMultipartOutput;
        private string _logContent = string.Empty;
        private string _statusMessage = "准备就绪";
        private int _progress;
        private bool _isProcessing;
        private CancellationTokenSource _cts;

        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }

        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                UpdateDefaultOutputPath();
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value)
                    ? value
                    : OutputDatasetUtils.NormalizeOutputPath(value, GetDefaultOutputName());

                SetProperty(ref _outputPath, normalized);
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        public bool CreateMultipartOutput
        {
            get => _createMultipartOutput;
            set => SetProperty(ref _createMultipartOutput, value);
        }

        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrWhiteSpace(OutputPath);

        public ICommand RefreshLayersCommand => new RelayCommand(RefreshLayers);
        public ICommand BrowseOutputCommand => new RelayCommand(BrowseOutput);
        public ICommand ShowHelpCommand => new RelayCommand(ShowHelp);
        public ICommand CancelCommand => new RelayCommand(() => _cts?.Cancel(), () => IsProcessing);
        public ICommand RunCommand => new RelayCommand(async () => await RunAsync(), () => CanProcess);

        public ExtractPolygonHolesViewModel()
        {
            RefreshLayers();
            UpdateDefaultOutputPath();
        }

        public async void RefreshLayers()
        {
            try
            {
                var layers = await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                        return new List<FeatureLayer>();

                    return map.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .Where(fl => fl.ShapeType == esriGeometryType.esriGeometryPolygon)
                        .ToList();
                });

                PolygonLayers.Clear();
                foreach (var layer in layers)
                    PolygonLayers.Add(layer);

                if (PolygonLayers.Count > 0)
                {
                    if (SelectedPolygonLayer == null || !PolygonLayers.Contains(SelectedPolygonLayer))
                        SelectedPolygonLayer = PolygonLayers[0];

                    AddLog($"已加载 {PolygonLayers.Count} 个面要素图层");
                }
                else
                {
                    SelectedPolygonLayer = null;
                    AddLog("当前地图中未找到面要素图层");
                }
            }
            catch (Exception ex)
            {
                AddLog($"刷新图层失败: {ex.Message}");
            }
        }

        private void UpdateDefaultOutputPath()
        {
            var defaultName = GetDefaultOutputName();
            var gdbPath = Project.Current?.DefaultGeodatabasePath;

            if (!string.IsNullOrWhiteSpace(gdbPath))
                OutputPath = Path.Combine(gdbPath, defaultName);
            else if (string.IsNullOrWhiteSpace(OutputPath))
                OutputPath = defaultName;
        }

        private string GetDefaultOutputName()
        {
            return SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_扣岛" : "提取面扣岛";
        }

        private void BrowseOutput()
        {
            try
            {
                var dialog = new SaveItemDialog
                {
                    Title = "选择输出面要素",
                    OverwritePrompt = true,
                    Filter = ItemFilters.FeatureClasses_All
                };

                var initialLocation = Project.Current?.DefaultGeodatabasePath;
                if (!string.IsNullOrWhiteSpace(initialLocation))
                    dialog.InitialLocation = initialLocation;

                if (dialog.ShowDialog() == true)
                    OutputPath = dialog.FilePath;
            }
            catch (Exception ex)
            {
                AddLog($"选择输出路径失败: {ex.Message}");
            }
        }

        private void ShowHelp()
        {
            AddLog("帮助: 提取输入面中的所有扣洞，输出面要素并继承源图层属性。可勾选将同一源要素的多个洞合并为一个多部件要素。");
        }

        private async Task RunAsync()
        {
            if (SelectedPolygonLayer == null)
            {
                AddLog("请选择输入面图层");
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                AddLog("请指定输出面图层");
                return;
            }

            IsProcessing = true;
            StatusMessage = "正在提取面扣岛...";
            Progress = 0;
            _cts = new CancellationTokenSource();

            try
            {
                OutputPath = OutputDatasetUtils.NormalizeOutputPath(OutputPath, GetDefaultOutputName());

                var outputInfo = OutputDatasetUtils.ParseOutputPath(OutputPath, $"{SelectedPolygonLayer.Name}_扣岛");
                AddLog($"输出路径: {outputInfo.CatalogPath}");

                Progress = 10;
                await OutputDatasetUtils.DeleteIfExistsAsync(outputInfo);

                var env = Geoprocessing.MakeEnvironmentArray("addOutputsToMap", "False", "overwriteoutput", "True");
                var outName = outputInfo.IsGdb ? outputInfo.OutNameNoExt : outputInfo.OutNameNoExt + ".shp";
                var createParams = Geoprocessing.MakeValueArray(
                    outputInfo.OutPathWorkspace,
                    outName,
                    "POLYGON",
                    SelectedPolygonLayer);

                var createResult = await Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", createParams, env, _cts.Token);
                if (createResult.IsFailed)
                {
                    AddLog("创建输出要素类失败:");
                    foreach (var message in createResult.Messages)
                        AddLog($" - {message.Text}");
                    StatusMessage = "处理失败";
                    return;
                }

                Progress = 30;
                var stats = await QueuedTask.Run(() => ExtractAndWriteHoles(outputInfo, _cts.Token));

                Progress = 100;
                StatusMessage = "提取完成";
                AddLog($"处理完成：扫描 {stats.SourceFeatureCount} 个要素，提取 {stats.HoleRingCount} 个扣洞，输出 {stats.OutputFeatureCount} 个要素。");

                if (stats.OutputFeatureCount == 0)
                    AddLog("未发现扣洞，已生成空输出要素类。");
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
                AddLog("操作已取消");
            }
            catch (Exception ex)
            {
                StatusMessage = "处理失败";
                AddLog($"处理失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private ExtractionStats ExtractAndWriteHoles(OutputDatasetUtils.OutputPathInfo outputInfo, CancellationToken token)
        {
            using var sourceFeatureClass = SelectedPolygonLayer.GetFeatureClass();

            if (outputInfo.IsGdb)
            {
                using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(outputInfo.GdbRoot)));
                using var outputFeatureClass = geodatabase.OpenDataset<FeatureClass>(outputInfo.RelativePathInGdb);
                return ExtractAndWriteCore(sourceFeatureClass, outputFeatureClass, token);
            }

            var outputFolder = Path.GetDirectoryName(outputInfo.CatalogPath);
            var outputName = Path.GetFileName(outputInfo.CatalogPath);
            using var datastore = new FileSystemDatastore(new FileSystemConnectionPath(new Uri(outputFolder), FileSystemDatastoreType.Shapefile));
            using var outputShp = datastore.OpenDataset<FeatureClass>(outputName);
            return ExtractAndWriteCore(sourceFeatureClass, outputShp, token);
        }

        private ExtractionStats ExtractAndWriteCore(FeatureClass sourceFeatureClass, FeatureClass outputFeatureClass, CancellationToken token)
        {
            var stats = new ExtractionStats();
            var sourceDefinition = sourceFeatureClass.GetDefinition();
            var outputDefinition = outputFeatureClass.GetDefinition();

            var fieldMappings = BuildFieldMappings(sourceDefinition, outputDefinition);
            var outputShapeField = outputDefinition.GetShapeField();

            using var sourceCursor = sourceFeatureClass.Search(new QueryFilter(), false);
            using var insertCursor = outputFeatureClass.CreateInsertCursor();

            while (sourceCursor.MoveNext())
            {
                token.ThrowIfCancellationRequested();

                var current = sourceCursor.Current;
                if (current is not Feature sourceFeature)
                {
                    current?.Dispose();
                    continue;
                }

                using (sourceFeature)
                {
                    stats.SourceFeatureCount++;

                    var polygon = sourceFeature.GetShape() as Polygon;
                    if (polygon == null || polygon.IsEmpty)
                        continue;

                    var holePolygons = ExtractHolePolygons(polygon);
                    if (holePolygons.Count == 0)
                        continue;

                    stats.HoleRingCount += holePolygons.Count;

                    if (CreateMultipartOutput)
                    {
                        var multipartHole = BuildMultipartPolygon(holePolygons, polygon.SpatialReference);
                        InsertPolygon(sourceFeature, multipartHole, outputShapeField, fieldMappings, outputFeatureClass, insertCursor);
                        stats.OutputFeatureCount++;
                        continue;
                    }

                    foreach (var holePolygon in holePolygons)
                    {
                        InsertPolygon(sourceFeature, holePolygon, outputShapeField, fieldMappings, outputFeatureClass, insertCursor);
                        stats.OutputFeatureCount++;
                    }
                }
            }

            insertCursor.Flush();
            return stats;
        }

        private static void InsertPolygon(
            Feature sourceFeature,
            Polygon outputPolygon,
            string outputShapeField,
            List<FieldMapping> fieldMappings,
            FeatureClass outputFeatureClass,
            InsertCursor insertCursor)
        {
            using var rowBuffer = outputFeatureClass.CreateRowBuffer();
            rowBuffer[outputShapeField] = outputPolygon;

            foreach (var mapping in fieldMappings)
            {
                try
                {
                    var value = sourceFeature[mapping.SourceFieldName];
                    if (value == null || value == DBNull.Value)
                        continue;

                    rowBuffer[mapping.OutputFieldName] = value;
                }
                catch
                {
                    // 目标字段可能不可编辑或类型不兼容，忽略后继续复制其他字段。
                }
            }

            insertCursor.Insert(rowBuffer);
        }

        private static List<FieldMapping> BuildFieldMappings(FeatureClassDefinition sourceDefinition, FeatureClassDefinition outputDefinition)
        {
            var sourceShapeField = sourceDefinition.GetShapeField();
            var sourceObjectIdField = sourceDefinition.GetObjectIDField();
            var outputShapeField = outputDefinition.GetShapeField();
            var outputObjectIdField = outputDefinition.GetObjectIDField();

            var sourceFields = sourceDefinition.GetFields()
                .Select(field => field.Name)
                .Where(name =>
                    !name.Equals(sourceShapeField, StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals(sourceObjectIdField, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var outputFields = outputDefinition.GetFields()
                .Select(field => field.Name)
                .Where(name =>
                    !name.Equals(outputShapeField, StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals(outputObjectIdField, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var mappings = new List<FieldMapping>();

            if (sourceFields.Count == outputFields.Count)
            {
                for (var i = 0; i < sourceFields.Count; i++)
                    mappings.Add(new FieldMapping(sourceFields[i], outputFields[i]));

                return mappings;
            }

            var outputSet = new HashSet<string>(outputFields, StringComparer.OrdinalIgnoreCase);
            foreach (var sourceField in sourceFields)
            {
                if (outputSet.Contains(sourceField))
                    mappings.Add(new FieldMapping(sourceField, sourceField));
            }

            return mappings;
        }

        private static List<Polygon> ExtractHolePolygons(Polygon polygon)
        {
            var simplifiedPolygon = GeometryEngine.Instance.SimplifyAsFeature(polygon) as Polygon ?? polygon;
            var ringGeometries = new List<RingGeometry>();

            foreach (var part in simplifiedPolygon.Parts)
            {
                var points = GetPartPoints(part);
                if (points.Count < 4)
                    continue;

                var signedArea = ComputeSignedArea(points);
                var area = Math.Abs(signedArea);
                if (area <= double.Epsilon)
                    continue;

                var ringPolygon = PolygonBuilderEx.CreatePolygon(points, simplifiedPolygon.SpatialReference);
                if (ringPolygon == null || ringPolygon.IsEmpty)
                    continue;

                ringGeometries.Add(new RingGeometry(ringPolygon, signedArea));
            }

            if (ringGeometries.Count == 0)
                return new List<Polygon>();

            var largestRing = ringGeometries.OrderByDescending(r => Math.Abs(r.SignedArea)).First();
            var exteriorSign = Math.Sign(largestRing.SignedArea);
            if (exteriorSign == 0)
                exteriorSign = -1;

            var holePolygons = new List<Polygon>();
            foreach (var ring in ringGeometries)
            {
                if (Math.Sign(ring.SignedArea) == exteriorSign)
                    continue;

                var simplifiedRing = GeometryEngine.Instance.SimplifyAsFeature(ring.RingPolygon) as Polygon ?? ring.RingPolygon;
                if (!simplifiedRing.IsEmpty)
                    holePolygons.Add(simplifiedRing);
            }

            return holePolygons;
        }

        private static Polygon BuildMultipartPolygon(IReadOnlyList<Polygon> polygons, SpatialReference spatialReference)
        {
            var builder = new PolygonBuilderEx(spatialReference);
            foreach (var polygon in polygons)
            {
                foreach (var part in polygon.Parts)
                {
                    var points = GetPartPoints(part);
                    if (points.Count >= 4)
                        builder.AddPart(points);
                }
            }

            var multipart = builder.ToGeometry();
            return GeometryEngine.Instance.SimplifyAsFeature(multipart) as Polygon ?? multipart;
        }

        private static double ComputeSignedArea(IReadOnlyList<MapPoint> points)
        {
            var count = points.Count;
            if (count < 3)
                return 0;

            if (count > 1 &&
                Math.Abs(points[0].X - points[count - 1].X) < 1e-12 &&
                Math.Abs(points[0].Y - points[count - 1].Y) < 1e-12)
            {
                count--;
            }

            if (count < 3)
                return 0;

            double sum = 0;
            for (var i = 0; i < count; i++)
            {
                var current = points[i];
                var next = points[(i + 1) % count];
                sum += current.X * next.Y - next.X * current.Y;
            }

            return sum * 0.5;
        }

        private static List<MapPoint> GetPartPoints(ReadOnlySegmentCollection part)
        {
            var points = new List<MapPoint>();
            if (part == null || part.Count == 0)
                return points;

            foreach (var segment in part)
                points.Add(segment.StartPoint);

            points.Add(part[part.Count - 1].EndPoint);
            return points;
        }

        private void AddLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            LogContent += $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
        }

        private sealed class ExtractionStats
        {
            public int SourceFeatureCount { get; set; }
            public int HoleRingCount { get; set; }
            public int OutputFeatureCount { get; set; }
        }

        private sealed class RingGeometry
        {
            public RingGeometry(Polygon ringPolygon, double signedArea)
            {
                RingPolygon = ringPolygon;
                SignedArea = signedArea;
            }

            public Polygon RingPolygon { get; }
            public double SignedArea { get; }
        }

        private sealed class FieldMapping
        {
            public FieldMapping(string sourceFieldName, string outputFieldName)
            {
                SourceFieldName = sourceFieldName;
                OutputFieldName = outputFieldName;
            }

            public string SourceFieldName { get; }
            public string OutputFieldName { get; }
        }
    }
}
