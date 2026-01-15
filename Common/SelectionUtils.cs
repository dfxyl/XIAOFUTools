using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Common
{
    /// <summary>
    /// 选择集通用工具。
    /// 注意：标注了“需在 MCT 调用”的方法，必须在 QueuedTask.Run 内部调用。
    /// </summary>
    public static class SelectionUtils
    {
        public class SelectionInfo
        {
            public bool HasSelection { get; init; }
            public int Count { get; init; }
            public string InfoText { get; init; }
        }

        /// <summary>
        /// 获取选择集信息（异步封装，内部切换至 MCT）。
        /// </summary>
        public static async Task<SelectionInfo> GetSelectionInfoAsync(FeatureLayer layer)
        {
            if (layer == null) return new SelectionInfo { HasSelection = false, Count = 0, InfoText = "全部要素" };

            int count = 0;
            await QueuedTask.Run(() =>
            {
                count = layer.SelectionCount;
            });

            return new SelectionInfo
            {
                HasSelection = count > 0,
                Count = count,
                InfoText = count > 0 ? $"已选 {count}" : "全部要素"
            };
        }

        /// <summary>
        /// 需在 MCT 调用：获取选择数量。
        /// </summary>
        public static int GetSelectionCount(FeatureLayer layer)
        {
            if (!QueuedTask.OnWorker)
                throw new InvalidOperationException("GetSelectionCount 必须在 QueuedTask(MCT) 中调用");
            if (layer == null) return 0;
            return layer.SelectionCount;
        }

        /// <summary>
        /// 需在 MCT 调用：根据 useSelection 返回选择集或全量游标。
        /// 调用者负责处置返回的 RowCursor。
        /// </summary>
        public static RowCursor GetSelectionOrAllCursor(FeatureLayer layer, bool useSelection, QueryFilter filter = null, bool recycling = false)
        {
            if (!QueuedTask.OnWorker)
                throw new InvalidOperationException("GetSelectionOrAllCursor 必须在 QueuedTask(MCT) 中调用");
            if (layer == null) return null;

            var table = layer.GetTable();
            if (table == null) return null;

            if (useSelection && layer.SelectionCount > 0)
                return layer.GetSelection().Search(filter ?? new QueryFilter(), recycling);

            return table.Search(filter ?? new QueryFilter(), recycling);
        }

        /// <summary>
        /// 根据是否存在选择集给出 UseSelection 的建议（UI 端可用）。
        /// </summary>
        public static bool RecommendUseSelection(bool currentUseSelection, bool hasSelection)
        {
            if (hasSelection && !currentUseSelection) return true; // 当出现选择集时建议开启
            if (!hasSelection && currentUseSelection) return false; // 无选择时建议关闭
            return currentUseSelection;
        }
    }
}
