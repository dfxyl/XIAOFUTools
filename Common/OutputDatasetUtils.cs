using System;
using System.IO;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace XIAOFUTools.Common
{
    /// <summary>
    /// 输出路径与数据集通用工具（统一识别与创建 GDB/SHP）。
    /// 注意：需在 ArcGIS Pro 的 MCT（QueuedTask.Run）上下文中调用涉及 ArcGIS.Core.Data 的方法。
    /// </summary>
    public static class OutputDatasetUtils
    {
        public class OutputPathInfo
        {
            public bool IsGdb { get; init; }
            public string GdbRoot { get; init; }              // 仅 GDB 下有效，例如 C:\data\a.gdb
            public string RelativePathInGdb { get; init; }    // 仅 GDB 下有效：例如 "FeatureDataset\\FC" 或 "FC"
            public string OutPathWorkspace { get; init; }     // CreateFeatureclass 的 out_path：GDB 时可为 gdb 或 gdb\FeatureDataset；SHP 为文件夹
            public string OutNameNoExt { get; init; }         // CreateFeatureclass 的 out_name（不带扩展）
            public string CatalogPath { get; init; }          // GP/显示使用的完整目录路径：GDB 为 C:\a.gdb\[FeatureDataset\]FC；SHP 为 C:\folder\name.shp
        }

        /// <summary>
        /// 规范化输出路径：
        /// - 包含 .gdb 视为 GDB；若缺少要素类名则补默认名
        /// - 非 .gdb 视为 Shapefile；若为目录或无扩展则补 .shp
        /// - 若传入 GDB 路径误带 .shp，则移除扩展
        /// </summary>
        public static string NormalizeOutputPath(string input, string defaultName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input)) return input;
                var path = input.Trim().Trim('"');

                // 相对路径原样返回
                if (!Path.IsPathRooted(path)) return path;

                var gdbIdx = path.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
                if (gdbIdx >= 0)
                {
                    var gdbRoot = path.Substring(0, gdbIdx + 4);
                    var rest = path.Length > gdbIdx + 4 ? path.Substring(gdbIdx + 4).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : string.Empty;

                    if (!string.IsNullOrEmpty(rest) && rest.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                        rest = rest.Substring(0, rest.Length - 4);

                    var name = string.IsNullOrEmpty(rest) ? (string.IsNullOrWhiteSpace(defaultName) ? "output" : defaultName) : rest;
                    return Path.Combine(gdbRoot, name);
                }

                // Shapefile 情况
                if (Directory.Exists(path) || path.EndsWith(Path.DirectorySeparatorChar.ToString()) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                {
                    var defName = string.IsNullOrWhiteSpace(defaultName) ? "output" : defaultName;
                    var shp = defName.EndsWith(".shp", StringComparison.OrdinalIgnoreCase) ? defName : defName + ".shp";
                    return Path.Combine(path, shp);
                }

                if (string.IsNullOrEmpty(Path.GetExtension(path)))
                {
                    return path + ".shp";
                }

                return path;
            }
            catch
            {
                return input;
            }
        }

        /// <summary>
        /// 解析输出路径，返回统一的信息对象。
        /// </summary>
        public static OutputPathInfo ParseOutputPath(string input, string defaultName)
        {
            if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("输出路径为空");
            var normalized = NormalizeOutputPath(input, defaultName);

            var gdbIdx = normalized.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
            if (gdbIdx >= 0)
            {
                var gdbRoot = normalized.Substring(0, gdbIdx + 4);
                var rest = normalized.Length > gdbIdx + 4 ? normalized.Substring(gdbIdx + 4).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : string.Empty;
                if (string.IsNullOrEmpty(rest)) rest = string.IsNullOrWhiteSpace(defaultName) ? "output" : defaultName;

                // 名称部分与（可选）要素数据集
                var restDir = Path.GetDirectoryName(rest); // 可能为 null
                var name = Path.GetFileName(rest);

                var outPathWorkspace = string.IsNullOrEmpty(restDir) ? gdbRoot : Path.Combine(gdbRoot, restDir);
                var catalogPath = Path.Combine(outPathWorkspace, name);

                // RelativePathInGdb 示例："FeatureDataset\\FC" 或 "FC"
                var relativePath = string.IsNullOrEmpty(restDir) ? name : Path.Combine(restDir, name);

                return new OutputPathInfo
                {
                    IsGdb = true,
                    GdbRoot = gdbRoot,
                    RelativePathInGdb = relativePath,
                    OutPathWorkspace = outPathWorkspace,
                    OutNameNoExt = name,
                    CatalogPath = catalogPath
                };
            }
            else
            {
                // Shapefile
                var folder = Path.GetDirectoryName(normalized) ?? Directory.GetCurrentDirectory();
                var fileName = Path.GetFileName(normalized);
                if (!fileName.EndsWith(".shp", StringComparison.OrdinalIgnoreCase)) fileName += ".shp";
                var nameNoExt = Path.GetFileNameWithoutExtension(fileName);

                return new OutputPathInfo
                {
                    IsGdb = false,
                    GdbRoot = null,
                    RelativePathInGdb = null,
                    OutPathWorkspace = folder,
                    OutNameNoExt = nameNoExt,
                    CatalogPath = Path.Combine(folder, fileName)
                };
            }
        }

        /// <summary>
        /// 检查输出是否已存在（GDB: 数据集；SHP: .shp 文件）。
        /// </summary>
        public static bool Exists(OutputPathInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (info.IsGdb)
            {
                try
                {
                    using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(info.GdbRoot)));
                    try
                    {
                        using var fc = gdb.OpenDataset<FeatureClass>(info.RelativePathInGdb);
                        return fc != null;
                    }
                    catch
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                return File.Exists(info.CatalogPath);
            }
        }

        /// <summary>
        /// 删除已存在的数据集或 Shapefile（使用 GP Delete_management）。
        /// </summary>
        public static async Task DeleteIfExistsAsync(OutputPathInfo info)
        {
            if (!Exists(info)) return;
            var deleteParams = Geoprocessing.MakeValueArray(info.CatalogPath);
            await Geoprocessing.ExecuteToolAsync("Delete_management", deleteParams);
        }

        /// <summary>
        /// 创建要素类（支持 GDB 或 SHP）。geometryType 如 "POINT" | "POLYLINE" | "POLYGON"。
        /// 可选 template：可传入 FeatureClass 的目录路径、图层对象等，以复制字段结构。
        /// 返回 GP 的 ReturnValue（目录路径）。
        /// </summary>
        public static async Task<string> CreateFeatureClassAsync(OutputPathInfo info, string geometryType, SpatialReference spatialReference, bool hasM = false, bool hasZ = false, object template = null)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (string.IsNullOrWhiteSpace(geometryType)) throw new ArgumentException("几何类型为空");

            var outName = info.IsGdb ? info.OutNameNoExt : info.OutNameNoExt + ".shp";
            var mFlag = hasM ? "ENABLED" : "DISABLED";
            var zFlag = hasZ ? "ENABLED" : "DISABLED";

            var parameters = Geoprocessing.MakeValueArray(
                info.OutPathWorkspace,
                outName,
                geometryType.ToUpperInvariant(),
                template,
                mFlag,
                zFlag,
                spatialReference
            );

            var result = await Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", parameters);
            if (result.IsFailed)
            {
                throw new Exception("CreateFeatureclass 失败: " + string.Join(", ", result.Messages));
            }
            return result.ReturnValue; // 例如 catalog 路径
        }
    }
}
