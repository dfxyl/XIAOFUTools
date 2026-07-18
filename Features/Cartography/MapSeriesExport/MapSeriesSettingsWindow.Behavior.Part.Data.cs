#define DEBUG
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    public partial class MapSeriesSettingsWindow
    {

    private async void LoadMapFramesAsync()
    {
        try
        {
            await QueuedTask.Run((Action)delegate
            {
                LayoutView active = LayoutView.Active;
                if (active != null)
                {
                    _currentLayout = active.Layout;
                    if (_currentLayout != null)
                    {
                        List<string> mapFrames = (
                            from mf in _currentLayout.Elements.OfType<MapFrame>()select ((Element)mf).Name).ToList();
                        ((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)delegate
                        {
                            MapFrameComboBox.Items.Clear();
                            foreach (string current in mapFrames)
                            {
                                MapFrameComboBox.Items.Add(current);
                            }

                            if (MapFrameComboBox.Items.Count > 0)
                            {
                                MapFrameComboBox.SelectedIndex = 0;
                            }
                        });
                    }
                }
            }, TaskCreationOptions.None);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            Debug.WriteLine("加载地图框失败: " + ex2.Message);
        }
    }

    private async void LoadIntersectLayersAsync()
    {
        try
        {
            List<string> layerNames = new List<string>();
            Dictionary<string, FeatureLayer> layerMap = new Dictionary<string, FeatureLayer>(StringComparer.OrdinalIgnoreCase);
            await QueuedTask.Run((Action)delegate
            {
                //IL_0127: Unknown result type (might be due to invalid IL or missing references)
                //IL_0135: Unknown result type (might be due to invalid IL or missing references)
                //IL_013f: Invalid comparison between Unknown and I4
                List<Map> list = new List<Map>();
                MapView active = MapView.Active;
                Map val = ((active != null) ? active.Map : null);
                if (val != null)
                {
                    list.Add(val);
                }

                LayoutView active2 = LayoutView.Active;
                Layout val2 = ((active2 != null) ? active2.Layout : null) ?? _currentLayout;
                if (val2 != null)
                {
                    foreach (MapFrame current in val2.Elements.OfType<MapFrame>())
                    {
                        if (current.Map != null && !list.Contains(current.Map))
                        {
                            list.Add(current.Map);
                        }
                    }
                }

                foreach (Map current2 in list)
                {
                    foreach (FeatureLayer current3 in current2.GetLayersAsFlattenedList().OfType<FeatureLayer>())
                    {
                        FeatureClass featureClass = current3.GetFeatureClass();
                        if (featureClass != null)
                        {
                            FeatureClassDefinition definition = featureClass.GetDefinition();
                            if ((int)((definition != null) ? new GeometryType? (definition.GetShapeType()) : ((GeometryType? )null)).GetValueOrDefault() == 27656 && !layerMap.ContainsKey(((MapMember)current3).Name))
                            {
                                layerMap[((MapMember)current3).Name] = current3;
                                layerNames.Add(((MapMember)current3).Name);
                            }
                        }
                    }
                }
            }, TaskCreationOptions.None);
            ((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)delegate
            {
                _intersectLayersByName.Clear();
                foreach (KeyValuePair<string, FeatureLayer> current in layerMap)
                {
                    _intersectLayersByName[current.Key] = current.Value;
                }

                IntersectLayerComboBox.Items.Clear();
                foreach (string current2 in layerNames.OrderBy((string name) => name))
                {
                    IntersectLayerComboBox.Items.Add(current2);
                }

                string text = ((!string.IsNullOrWhiteSpace(_pendingIntersectLayerName)) ? _pendingIntersectLayerName : IntersectLayerComboBox.SelectedItem?.ToString());
                if (!string.IsNullOrWhiteSpace(text) && IntersectLayerComboBox.Items.Contains(text))
                {
                    IntersectLayerComboBox.SelectedItem = text;
                }
                else if (IntersectLayerComboBox.Items.Count > 0 && IntersectLayerComboBox.SelectedIndex < 0)
                {
                    IntersectLayerComboBox.SelectedIndex = 0;
                }
            });
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            Debug.WriteLine("加载交集图层失败: " + ex2.Message);
        }
    }

    private async void LoadIntersectFieldsAsync(string layerName)
    {
        try
        {
            List<string> fieldNames = new List<string>();
            if (!string.IsNullOrWhiteSpace(layerName) && _intersectLayersByName.TryGetValue(layerName, out var layer))
            {
                await QueuedTask.Run((Action)delegate
                {
                    //IL_0045: Unknown result type (might be due to invalid IL or missing references)
                    //IL_004b: Invalid comparison between Unknown and I4
                    //IL_004f: Unknown result type (might be due to invalid IL or missing references)
                    //IL_0055: Invalid comparison between Unknown and I4
                    //IL_0059: Unknown result type (might be due to invalid IL or missing references)
                    //IL_0060: Invalid comparison between Unknown and I4
                    //IL_0064: Unknown result type (might be due to invalid IL or missing references)
                    //IL_006a: Invalid comparison between Unknown and I4
                    //IL_006e: Unknown result type (might be due to invalid IL or missing references)
                    //IL_0075: Invalid comparison between Unknown and I4
                    FeatureClass featureClass = layer.GetFeatureClass();
                    object obj;
                    if (featureClass == null)
                    {
                        obj = null;
                    }
                    else
                    {
                        FeatureClassDefinition definition = featureClass.GetDefinition();
                        obj = ((definition != null) ? ((TableDefinition)definition).GetFields() : null);
                    }

                    IReadOnlyList<Field> readOnlyList = (IReadOnlyList<Field>)obj;
                    if (readOnlyList == null)
                    {
                        return;
                    }

                    foreach (Field current in readOnlyList)
                    {
                        if ((int)current.FieldType != 7 && (int)current.FieldType != 6 && (int)current.FieldType != 11 && (int)current.FieldType != 8 && (int)current.FieldType != 9)
                        {
                            fieldNames.Add(current.Name);
                        }
                    }
                }, TaskCreationOptions.None);
            }

            ((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)delegate
            {
                IntersectFieldComboBox.Items.Clear();
                foreach (string current in fieldNames)
                {
                    IntersectFieldComboBox.Items.Add(current);
                }

                if (!string.IsNullOrWhiteSpace(_pendingIntersectFieldName) && IntersectFieldComboBox.Items.Contains(_pendingIntersectFieldName))
                {
                    IntersectFieldComboBox.SelectedItem = _pendingIntersectFieldName;
                }
                else if (IntersectFieldComboBox.Items.Count > 0)
                {
                    IntersectFieldComboBox.SelectedIndex = 0;
                }
            });
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            Debug.WriteLine("加载交集字段失败: " + ex2.Message);
        }
    }
    }
}
