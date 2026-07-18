using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.IntersectSummary
{
    internal partial class IntersectSummaryDockPaneViewModel
    {

        /// <summary>
        /// 按区域和类别汇总结果
        /// </summary>
        private List<IntersectSummaryResultItem> AggregateResults(
            List<IntersectSummaryResultItem> results,
            List<string> regionFields,
            List<string> classFields)
        {
            var grouped = results.GroupBy(r =>
            {
                var regionKey = string.Join("|", regionFields.Select(f => r.RegionValues.ContainsKey(f) ? (r.RegionValues[f]?.ToString() ?? "") : ""));
                var classKey = string.Join("|", classFields.Select(f => r.ClassValues.ContainsKey(f) ? (r.ClassValues[f]?.ToString() ?? "") : ""));
                return $"{regionKey}||{classKey}";
            });

            var aggregated = new List<IntersectSummaryResultItem>();

            foreach (var group in grouped)
            {
                var first = group.First();
                var item = new IntersectSummaryResultItem
                {
                    RegionValues = new Dictionary<string, object>(first.RegionValues),
                    ClassValues = new Dictionary<string, object>(first.ClassValues),
                    Area = group.Sum(g => g.Area),
                    AdjustedArea = group.Sum(g => g.AdjustedArea)
                };
                aggregated.Add(item);
            }

            return aggregated;
        }
    }
}
