using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Shared;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.DataManagement.RangeClipTool
{
    internal partial class RangeClipToolViewModel
    {

        /// <summary>
        /// 创建范围几何体
        /// </summary>
        private Task<Geometry> CreateRangeGeometry(object rangeValue)
        {
            try
            {
                var featureClass = SelectedRangeLayer.GetFeatureClass();
                var queryFilter = new QueryFilter();

                // 构建查询条件
                if (rangeValue == null)
                {
                    queryFilter.WhereClause = $"{SelectedRangeField} IS NULL";
                }
                else if (rangeValue is string)
                {
                    var strVal = rangeValue.ToString()?.Replace("'", "''");
                    queryFilter.WhereClause = $"{SelectedRangeField} = '{strVal}'";
                }
                else
                {
                    queryFilter.WhereClause = $"{SelectedRangeField} = {rangeValue}";
                }

                var geometries = new List<Geometry>();

                using (var cursor = featureClass.Search(queryFilter, false))
                {
                    while (cursor.MoveNext())
                    {
                        if (CancelRequested) break;

                        using (var feature = cursor.Current as Feature)
                        {
                            if (feature != null)
                            {
                                var geometry = feature.GetShape();
                                if (geometry != null)
                                {
                                    geometries.Add(geometry);
                                }
                            }
                        }
                    }
                }

                if (geometries.Count == 0)
                {
                    return Task.FromResult<Geometry>(null);
                }

                // 如果只有一个几何体，直接返回
                if (geometries.Count == 1)
                {
                    return Task.FromResult(geometries[0]);
                }

                // 合并多个几何体
                return Task.FromResult(GeometryEngine.Instance.Union(geometries));
            }
            catch (Exception ex)
            {
                LogError($"创建范围几何体时出错: {ex.Message}");
                return Task.FromResult<Geometry>(null);
            }
        }
    }
}
