using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure
{
    internal sealed class ArcGisOvertureMapMemberCleanupService : IOvertureMapMemberCleanupService
    {
        public Task<int> RemoveFromActiveMapUsingFolderAsync(
            string folderPath,
            CancellationToken cancellationToken)
        {
            return QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var map = MapView.Active?.Map;
                return map == null
                    ? 0
                    : RemoveMatchingMembers(
                        map,
                        path => OverturePathScope.IsWithinFolder(path, folderPath),
                        cancellationToken);
            });
        }

        public Task<int> RemoveFromProjectMapsUsingFileAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            return QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var project = Project.Current;
                if (project == null)
                {
                    return 0;
                }

                var removedCount = 0;
                var maps = project.GetItems<MapProjectItem>()
                    .Select(item => item.GetMap())
                    .Where(map => map != null)
                    .ToList();
                foreach (var map in maps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    removedCount += RemoveMatchingMembers(
                        map,
                        path => OverturePathScope.RefersToFile(path, filePath),
                        cancellationToken);
                }

                return removedCount;
            });
        }

        private static int RemoveMatchingMembers(
            Map map,
            Func<string, bool> matchesPath,
            CancellationToken cancellationToken)
        {
            var membersToRemove = new List<MapMember>();
            foreach (var featureLayer in map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var featureClass = featureLayer.GetFeatureClass();
                    var path = featureClass?.GetPath();
                    if (path?.IsFile == true && matchesPath(path.LocalPath))
                    {
                        membersToRemove.Add(featureLayer);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"检查 Overture 图层 '{featureLayer.Name}' 数据源失败: {ex.Message}");
                }
            }

            foreach (var standaloneTable in map
                .GetStandaloneTablesAsFlattenedList()
                .OfType<StandaloneTable>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var table = standaloneTable.GetTable();
                    var path = table?.GetPath();
                    if (path?.IsFile == true && matchesPath(path.LocalPath))
                    {
                        membersToRemove.Add(standaloneTable);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"检查 Overture 独立表 '{standaloneTable.Name}' 数据源失败: {ex.Message}");
                }
            }

            var removedCount = 0;
            foreach (var member in membersToRemove.Distinct())
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (member)
                {
                    case Layer layer:
                        map.RemoveLayer(layer);
                        (layer as IDisposable)?.Dispose();
                        removedCount++;
                        break;
                    case StandaloneTable table:
                        map.RemoveStandaloneTable(table);
                        (table as IDisposable)?.Dispose();
                        removedCount++;
                        break;
                }
            }

            return removedCount;
        }
    }
}
