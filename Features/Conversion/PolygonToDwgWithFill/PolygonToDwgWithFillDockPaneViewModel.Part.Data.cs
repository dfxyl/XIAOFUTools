using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Conversion.PolygonToDwgWithFill
{
    internal partial class PolygonToDwgWithFillDockPaneViewModel
    {
        private void LoadNamingFieldsAsync(FeatureLayer featureLayer)
        {
            NamingFields?.Clear();
            if (featureLayer == null)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var fields = await QueuedTask.Run(() =>
                    {
                        using var table = featureLayer.GetTable();
                        var definition = table?.GetDefinition();
                        return definition?.GetFields()
                            .Where(field => field != null &&
                                            field.FieldType != FieldType.Geometry &&
                                            field.FieldType != FieldType.OID &&
                                            field.FieldType != FieldType.GlobalID &&
                                            field.FieldType != FieldType.GUID)
                            .Select(field => (
                                Name: field.Name,
                                Alias: string.IsNullOrWhiteSpace(field.AliasName) ? field.Name : field.AliasName))
                            .ToList() ?? new List<(string Name, string Alias)>();
                    });

                    await PresentationServices.UiThread.InvokeAsync(() =>
                    {
                        NamingFields.Clear();
                        foreach (var field in fields)
                        {
                            NamingFields.Add(new CadFieldOption
                            {
                                Name = field.Name,
                                Alias = field.Alias,
                                IsSelected = false
                            });
                        }
                    });
                }
                catch
                {
                    // 字段列表读取失败不影响主导出流程。
                }
            });
        }
    }
}
