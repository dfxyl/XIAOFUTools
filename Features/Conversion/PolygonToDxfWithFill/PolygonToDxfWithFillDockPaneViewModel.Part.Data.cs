using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Conversion.PolygonToDxfWithFill
{
    internal partial class PolygonToDxfWithFillDockPaneViewModel
    {

        // 加载当前图层的字段列表，供命名字段多选
        private void LoadNamingFieldsAsync(FeatureLayer fl)
        {
            NamingFields.Clear();
            if (fl == null) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var items = await QueuedTask.Run(() =>
                    {
                        try
                        {
                            using var table = fl.GetTable();
                            if (table == null) return new List<(string Name, string Alias)>();
                            var def = table.GetDefinition();
                            var fields = def.GetFields();
                            var list = new List<(string Name, string Alias)>();
                            foreach (var f in fields)
                            {
                                if (f == null) continue;
                                // 过滤几何OID等字段
                                var ft = f.FieldType;
                                if (ft == FieldType.Geometry || ft == FieldType.OID || ft == FieldType.GlobalID || ft == FieldType.GUID)
                                    continue;
                                string alias = null;
                                try { alias = f.AliasName; } catch { alias = null; }
                                if (string.IsNullOrWhiteSpace(alias)) alias = f.Name;
                                list.Add((f.Name, alias));
                            }
                            return list;
                        }
                        catch { return new List<(string Name, string Alias)>(); }
                    });

                    await PresentationServices.UiThread.InvokeAsync(() =>
                    {
                        NamingFields.Clear();
                        foreach (var it in items)
            NamingFields.Add(new CadFieldOption { Name = it.Name, Alias = it.Alias, IsSelected = false });
                    });
                }
                catch { }
            });
        }

    }
}
