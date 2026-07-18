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

namespace XIAOFUTools.Features.Editing.ChineseNumbering
{
    internal partial class ChineseNumberingViewModel
    {

        /// <summary>
        /// 异步加载图层，不阻塞UI线程
        /// </summary>
        private void LoadLayersAsync()
        {
            Task.Run(async () =>
            {
                try
                {
                    var tempLayers = new List<FeatureLayer>();

                    await QueuedTask.Run(() =>
                    {
                        var map = MapView.Active?.Map;
                        if (map != null)
                        {
                            var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>();
                            foreach (var layer in layers)
                            {
                                tempLayers.Add(layer);
                            }
                        }
                    });

                    // 在UI线程更新图层列表
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                            // 清空图层列表
                            LayerList?.Clear();

                            // 添加图层
                            if (LayerList != null)
                            {
                                foreach (var layer in tempLayers)
                                {
                                    LayerList.Add(layer);
                                }

                                // 如果有图层，默认选择第一个
                                if (LayerList.Count > 0 && SelectedLayer == null)
                                {
                                    SelectedLayer = LayerList[0];
                                }
                                // 更新一次选择信息
                                UpdateSelectionInfo();
                            }
                    });
                }
                catch (Exception ex)
                {
                    // 确保在UI线程显示错误信息
                    PresentationServices.UiThread.InvokeOrRun(() =>
                        PresentationServices.Dialogs.Show($"加载图层时出错: {ex.Message}", "错误"));
                }
            });
        }

        /// <summary>
        /// 加载所选图层的字段
        /// </summary>
        private void LoadFields()
        {
            if (SelectedLayer == null)
            {
                FieldList?.Clear();
                GroupFieldList?.Clear();
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var tempFieldInfos = new List<FieldInfo>();

                    await QueuedTask.Run(() =>
                    {
                        var table = SelectedLayer.GetTable();
                        if (table != null)
                        {
                            var definition = table.GetDefinition();
                            var fields = definition.GetFields();

                            foreach (var field in fields)
                            {
                                // 添加文本和数字类型的字段
                                if (field.FieldType == FieldType.String ||
                                    field.FieldType == FieldType.SmallInteger ||
                                    field.FieldType == FieldType.Integer ||
                                    field.FieldType == FieldType.Double)
                                {
                                    var fieldInfo = new FieldInfo
                                    {
                                        Name = field.Name,
                                        Alias = field.AliasName
                                    };
                                    tempFieldInfos.Add(fieldInfo);
                                }
                            }
                        }
                    });

                    // 在UI线程更新字段列表
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                            FieldList?.Clear();
                            GroupFieldList?.Clear();

                            if (FieldList != null && GroupFieldList != null)
                            {
                                // 先向分组字段列表添加一个空选项
                                GroupFieldList.Add("(不分组)");

                                // 更新字段列表
                                foreach (var fieldInfo in tempFieldInfos)
                                {
                                    FieldList.Add(fieldInfo);
                                    GroupFieldList.Add(fieldInfo);
                                }

                                // 默认选择"不分组"选项
                                SelectedGroupField = "(不分组)";
                            }
                    });
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                        PresentationServices.Dialogs.Show($"加载字段时出错: {ex.Message}", "错误"));
                }
            });
        }

        /// <summary>
        /// 将数字转换为中文数字
        /// </summary>
        private string ConvertToChinese(int number)
        {
            if (number <= 0)
                return string.Empty;
                
            string[] chineseNumbers = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            
            if (number < 10)
            {
                return chineseNumbers[number];
            }
            else if (number < 100)
            {
                int tens = number / 10;
                int ones = number % 10;
                
                if (tens == 1)
                {
                    return ones == 0 ? "十" : "十" + chineseNumbers[ones];
                }
                else
                {
                    return chineseNumbers[tens] + "十" + (ones == 0 ? "" : chineseNumbers[ones]);
                }
            }
            else
            {
                // 大于100的数字处理
                int hundreds = number / 100;
                int tensAndOnes = number % 100;
                int tens = tensAndOnes / 10;
                int ones = tensAndOnes % 10;
                
                string result = chineseNumbers[hundreds] + "百";
                
                if (tens == 0 && ones == 0)
                {
                    return result;
                }
                else if (tens == 0)
                {
                    return result + "零" + chineseNumbers[ones];
                }
                else
                {
                    if (tens == 1)
                    {
                        return result + (ones == 0 ? "一十" : "一十" + chineseNumbers[ones]);
                    }
                    else
                    {
                        return result + chineseNumbers[tens] + "十" + (ones == 0 ? "" : chineseNumbers[ones]);
                    }
                }
            }
        }
    }
}
