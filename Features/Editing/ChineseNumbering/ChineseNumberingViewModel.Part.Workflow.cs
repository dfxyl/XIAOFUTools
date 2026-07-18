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
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Editing.ChineseNumbering
{
    internal partial class ChineseNumberingViewModel
    {

        /// <summary>
        /// 执行编号操作
        /// </summary>
        private async void ExecuteNumbering()
        {
            if (!CanExecuteNumbering())
                return;

            try
            {
                using (PresentationServices.ProgressDialogs.Show("正在执行中文编号..."))
                {

                // 在后台线程执行编号操作
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        // 创建编辑操作
                        var editOperation = new EditOperation();
                        editOperation.Name = "地块中文编号";
                        
                        // 获取选中图层的要素表格
                        var featureTable = _selectedLayer.GetTable();
                        
                        // 准备查询
                        var queryFilter = new QueryFilter();
                        
                        // 如果只编号空记录，添加过滤条件
                        if (_onlyEmptyRecords)
                        {
                            queryFilter.WhereClause = $"{_selectedNumberField.Name} IS NULL OR {_selectedNumberField.Name} = ''";
                        }
                        
                        // 根据是否存在选择集决定数据源：优先在选择集中搜索
                        bool useSelection = _useSelection && _selectedLayer.SelectionCount > 0;
                        using (var rowCursor = useSelection
                            ? _selectedLayer.GetSelection().Search(queryFilter, false)
                            : featureTable.Search(queryFilter, false))
                        {
                            string groupFieldName = null;
                            // 检查是否有分组字段并且不是"不分组"选项
                            if (_selectedGroupField is FieldInfo fieldInfo)
                            {
                                groupFieldName = fieldInfo.Name;
                            }
                                                 
                            if (!string.IsNullOrEmpty(groupFieldName))
                            {
                                // 按分组编号
                                // 获取分组字段值和对应的要素OID
                                var groupValues = new Dictionary<string, List<long>>();
                                
                                while (rowCursor.MoveNext())
                                {
                                    using (var row = rowCursor.Current)
                                    {
                                        // 获取分组字段值
                                        string groupValue = "DefaultGroup"; // 默认组
                                        
                                        var groupValueObj = row[groupFieldName];
                                        groupValue = groupValueObj?.ToString() ?? "DefaultGroup";
                                        
                                        // 添加到分组字典
                                        if (!groupValues.ContainsKey(groupValue))
                                        {
                                            groupValues[groupValue] = new List<long>();
                                        }
                                        
                                        groupValues[groupValue].Add(row.GetObjectID());
                                    }
                                }
                                
                                // 对每个分组分别编号
                                foreach (var group in groupValues)
                                {
                                    int currentNumber = _startNumber;
                                    
                                    foreach (var objectId in group.Value)
                                    {
                                        // 转换为中文数字
                                        string chineseNumber = ConvertToChinese(currentNumber);
                                        
                                        // 构建最终编号
                                        string finalNumber = $"{_prefix}{chineseNumber}{_suffix}";
                                        
                                        // 使用EditOperation来更新字段值
                                        editOperation.Modify(featureTable, objectId, 
                                            new Dictionary<string, object> { { _selectedNumberField.Name, finalNumber } });
                                        
                                        currentNumber++;
                                    }
                                }
                            }
                            else
                            {
                                // 不分组编号，按原方式处理
                                // 获取所有符合条件的要素OID
                                var objectIds = new List<long>();
                                
                                while (rowCursor.MoveNext())
                                {
                                    using (var row = rowCursor.Current)
                                    {
                                        objectIds.Add(row.GetObjectID());
                                    }
                                }
                                
                                // 为每个要素编号
                                for (int i = 0; i < objectIds.Count; i++)
                                {
                                    // 计算当前号码
                                    int currentNumber = _startNumber + i;
                                    
                                    // 转换为中文数字
                                    string chineseNumber = ConvertToChinese(currentNumber);
                                    
                                    // 构建最终编号
                                    string finalNumber = $"{_prefix}{chineseNumber}{_suffix}";
                                    
                                    // 使用EditOperation来更新字段值
                                    editOperation.Modify(featureTable, objectIds[i], 
                                        new Dictionary<string, object> { { _selectedNumberField.Name, finalNumber } });
                                }
                            }
                        }
                        
                        // 执行编辑操作
                        bool result = editOperation.Execute();
                        if (!result)
                        {
                            throw new Exception("编号操作执行失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        PresentationServices.Dialogs.Show("执行中文编号时出错: " + ex.Message, "错误");
                    }
                });

                }

                // 刷新地图视图
                await QueuedTask.Run(() => 
                {
                    if (_selectedLayer != null)
                    {
                        _selectedLayer.ClearSelection();
                        // 直接刷新整个地图视图
                        if (MapView.Active != null)
                        {
                            MapView.Active.Redraw(true);
                        }
                    }
                });
                
                // 显示成功消息
                PresentationServices.Dialogs.Show("地块中文编号操作已完成！", "完成");
                
                // 关闭窗口
                CloseWindow();
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show("执行中文编号时出错: " + ex.Message, "错误");
            }
        }

        /// <summary>
        /// 判断是否可以执行编号
        /// </summary>
        private bool CanExecuteNumbering()
        {
            // 必须选择图层和编号字段
            return _selectedLayer != null && _selectedNumberField != null;
        }
    }
}
