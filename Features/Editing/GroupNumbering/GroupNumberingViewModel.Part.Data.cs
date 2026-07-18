using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;

namespace XIAOFUTools.Features.Editing.GroupNumbering
{
    internal partial class GroupNumberingViewModel
    {

        /// <summary>
        /// 异步加载图层，不阻塞UI线程
        /// </summary>
        private async void LoadLayersAsync()
        {
            try
            {
                // 使用QueuedTask在后台线程执行
                var featureLayers = await QueuedTask.Run(() =>
                {
                    var tempLayers = new List<FeatureLayer>();
                    
                    // 获取当前活动地图视图
                    var mapView = MapView.Active;
                    if (mapView == null || mapView.Map == null)
                    {
                        return tempLayers;
                    }

                    // 获取地图中的所有图层
                    var allLayers = mapView.Map.GetLayersAsFlattenedList();
                    if (allLayers == null || !allLayers.Any())
                    {
                        return tempLayers;
                    }

                    // 筛选出要素图层
                    var layers = allLayers.OfType<FeatureLayer>().ToList();
                    if (layers != null && layers.Any())
                    {
                        tempLayers.AddRange(layers);
                    }
                    
                    return tempLayers;
                });

                // 在UI线程上更新ObservableCollection
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    // 清空当前列表
                    _layerList.Clear();
                    
                    // 添加图层到列表
                    foreach (var layer in featureLayers)
                    {
                        _layerList.Add(layer);
                    }
                    
                    // 通知属性变化
                    NotifyPropertyChanged(() => LayerList);
                    
                    // 如果列表不为空，选择第一个图层
                    if (_layerList.Count > 0 && _selectedLayer == null)
                    {
                        SelectedLayer = _layerList[0];
                    }

                    // 更新一次选择信息（即使未选择图层也会显示默认提示）
                    UpdateSelectionInfo();
                });
            }
            catch (Exception ex)
            {
                // 捕获并显示加载图层时的任何异常
                PresentationServices.Dialogs.Show($"加载图层时出错: {ex.Message}", "错误");
            }

            // 通知界面更新
            NotifyPropertyChanged(() => LayerList);
        }

        /// <summary>
        /// 加载所选图层的字段
        /// </summary>
        private async void LoadFields()
        {
            // 检查所选图层是否为null
            if (_selectedLayer == null)
                return;

            try
            {
                // 先通知UI更新字段列表
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    // 清空通用字段列表
                    if (_fieldList == null)
                    {
                        _fieldList = new ObservableCollection<FieldInfo>();
                    }
                    else
                    {
                        _fieldList.Clear();
                    }
                    
                    // 清空分组字段列表
                    if (_groupFieldList == null)
                    {
                        _groupFieldList = new ObservableCollection<object>();
                    }
                    else
                    {
                        _groupFieldList.Clear();
                    }
                    
                    // 清空编号字段列表
                    if (_numberFieldList == null)
                    {
                        _numberFieldList = new ObservableCollection<FieldInfo>();
                    }
                    else
                    {
                        _numberFieldList.Clear();
                    }
                    
                    // 清空当前选择的字段
                    _selectedGroupField = null;
                    _selectedNumberField = null;
                    
                    NotifyPropertyChanged(() => FieldList);
                    NotifyPropertyChanged(() => GroupFieldList);
                    NotifyPropertyChanged(() => NumberFieldList);
                    NotifyPropertyChanged(() => SelectedGroupField);
                    NotifyPropertyChanged(() => SelectedNumberField);
                });

                // 在后台线程中获取字段
                var fieldInfos = await QueuedTask.Run(() =>
                {
                    var tempFields = new List<FieldInfo>();
                    
                    // 获取图层的字段信息
                    var table = _selectedLayer.GetTable();
                    if (table == null)
                        return tempFields;
                        
                    var definition = table.GetDefinition();
                    if (definition == null)
                        return tempFields;
                        
                    var fields = definition.GetFields();
                    if (fields == null)
                        return tempFields;

                    // 添加支持的字段类型
                    foreach (var field in fields)
                    {
                        if (field == null)
                            continue;
                            
                        // 添加文本和数字类型的字段
                        if (field.FieldType == FieldType.String || 
                            field.FieldType == FieldType.SmallInteger || 
                            field.FieldType == FieldType.Integer ||
                            field.FieldType == FieldType.Double)
                        {
                            tempFields.Add(new FieldInfo 
                            { 
                                Name = field.Name, 
                                Alias = field.AliasName 
                            });
                        }
                    }
                    
                    return tempFields;
                });
                
                // 在UI线程上更新字段列表
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    // 先向分组字段列表添加一个空选项
                    _groupFieldList.Add("(不分组)");
                    
                    // 更新所有字段列表
                    foreach (var fieldInfo in fieldInfos)
                    {
                        _fieldList.Add(fieldInfo);
                        _groupFieldList.Add(fieldInfo);
                        _numberFieldList.Add(fieldInfo);
                    }
                    
                    // 通知界面更新
                    NotifyPropertyChanged(() => FieldList);
                    NotifyPropertyChanged(() => GroupFieldList);
                    NotifyPropertyChanged(() => NumberFieldList);
                    
                    // 默认选择"不分组"选项
                    _selectedGroupField = "(不分组)";
                    NotifyPropertyChanged(() => SelectedGroupField);
                });
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show($"加载字段时出错: {ex.Message}", "错误");
            }
        }
    }
}
