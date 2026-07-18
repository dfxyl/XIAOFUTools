using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Editing.Boundary.BoundaryPointLineGenerator
{
    internal partial class BoundaryPointLineGeneratorDockPaneViewModel
    {
        private async Task AddJZDFields(string featureClassPath)
        {
            try
            {
                // 添加字段并设置中文别名
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "BSM", "LONG", null, null, null, "标识码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "ZDZHDM", "TEXT", null, null, null, "宗地/宗海代码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "YSDM", "TEXT", null, null, 10, "要素代码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZDH", "TEXT", null, null, 10, "界址点号"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "SXH", "LONG", null, null, null, "顺序号"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JBLX", "TEXT", null, null, 2, "界标类型"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZDLX", "TEXT", null, null, 2, "界址点类型"));
                // 坐标字段修正为 XZBZ/YZBZ 并按测绘习惯反转 XY 写入（在生成时处理）
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "XZBZ", "DOUBLE", 15, 3, null, "X坐标值"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "YZBZ", "DOUBLE", 15, 3, null, "Y坐标值"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "ZZBZ", "DOUBLE", 15, 3, null, "Z坐标值"));
            }
            catch (Exception ex) { LogError($"添加JZD字段失败: {ex.Message}"); }
        }
        private async Task AddJZXFields(string featureClassPath)
        {
            try
            {
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "BSM", "LONG", null, null, null, "标识码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "ZDZHDM", "TEXT", null, null, null, "宗地/宗海代码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "YSDM", "TEXT", null, null, 10, "要素代码"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZXCD", "DOUBLE", 15, 2, null, "界址线长度"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZXLB", "TEXT", null, null, 2, "界址线类别"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZXWZ", "TEXT", null, null, 1, "界址线位置"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "JZXZ", "TEXT", null, null, 6, "界址线质"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "QSJXYSBH", "TEXT", null, null, 30, "权属界线协议书编号"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "QSJXYS", "BLOB", null, null, null, "权属界线协议书"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "QSZYYSBH", "TEXT", null, null, 30, "权属争议原由书编号"));
                await Geoprocessing.ExecuteToolAsync("AddField_management", Geoprocessing.MakeValueArray(featureClassPath, "QSZYYS", "BLOB", null, null, null, "权属争议原由书"));
            }
            catch (Exception ex) { LogError($"添加JZX字段失败: {ex.Message}"); }
        }

        private void TrySet(RowBuffer buf, string field, object value)
        {
            try { buf[field] = value ?? (object)DBNull.Value; } catch { }
        }
        private void Append(string msg)
        {
            _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            PresentationServices.UiThread.InvokeOrRun(() => LogContent = _logBuilder.ToString());
        }
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
    }
}
