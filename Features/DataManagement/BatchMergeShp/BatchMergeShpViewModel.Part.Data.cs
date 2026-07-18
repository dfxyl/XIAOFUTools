using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.BatchMergeShp
{
    internal partial class BatchMergeShpViewModel
    {

        private static string GetGeometryKind(string shpPath)
        {
            try
            {
                using var stream = new FileStream(shpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length < 36)
                {
                    return "Unknown";
                }

                stream.Seek(32, SeekOrigin.Begin);
                var shapeTypeBuffer = new byte[4];
                var bytesRead = stream.Read(shapeTypeBuffer, 0, shapeTypeBuffer.Length);
                if (bytesRead != shapeTypeBuffer.Length)
                {
                    return "Unknown";
                }

                int shapeType = BitConverter.ToInt32(shapeTypeBuffer, 0);
                return shapeType switch
                {
                    1 or 11 or 21 => "Point",
                    3 or 13 or 23 => "Polyline",
                    5 or 15 or 25 => "Polygon",
                    8 or 18 or 28 => "Multipoint",
                    31 => "Multipatch",
                    _ => "Unknown"
                };
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string GetGeometryDisplayName(string geometryKind)
        {
            return geometryKind switch
            {
                "Point" => "点",
                "Polyline" => "线",
                "Polygon" => "面",
                "Multipoint" => "多点",
                "Multipatch" => "多面体",
                _ => "未知"
            };
        }

        private async Task<HashSet<string>> GetFieldNameSetAsync(string catalogPath)
        {
            return await QueuedTask.Run(() =>
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (catalogPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                {
                    var folder = Path.GetDirectoryName(catalogPath) ?? string.Empty;
                    var shpName = Path.GetFileName(catalogPath);
                    var connectionPath = new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile);
                    using var datastore = new FileSystemDatastore(connectionPath);
                    using var featureClass = datastore.OpenDataset<FeatureClass>(shpName);
                    foreach (var field in featureClass.GetDefinition().GetFields())
                    {
                        names.Add(field.Name);
                    }

                    return names;
                }

                int gdbIndex = catalogPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
                if (gdbIndex < 0)
                {
                    return names;
                }

                var gdbRoot = catalogPath.Substring(0, gdbIndex + 4);
                var relativePath = catalogPath.Substring(gdbIndex + 4).TrimStart('\\', '/').Replace('/', '\\');
                using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbRoot)));
                using var featureClassInGdb = geodatabase.OpenDataset<FeatureClass>(relativePath);
                foreach (var field in featureClassInGdb.GetDefinition().GetFields())
                {
                    names.Add(field.Name);
                }

                return names;
            });
        }

        private static string GetGpMessageText(IGPResult result)
        {
            try
            {
                if (result?.Messages != null && result.Messages.Any())
                {
                    return string.Join("; ", result.Messages);
                }
            }
            catch
            {
                // ignored
            }

            return "未知错误";
        }
    }
}
