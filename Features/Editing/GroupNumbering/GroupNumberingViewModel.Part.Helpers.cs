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
        /// 初始化基本属性
        /// </summary>
        private void Initialize()
        {
            // 初始化默认值
            _prefix = string.Empty;
            _suffix = string.Empty;
            _onlyEmptyRecords = false;
            _startNumber = 1; // 默认起始号码为1
            
            // 确保在UI线程上初始化集合
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                // 初始化空的图层列表
                _layerList = new ObservableCollection<FeatureLayer>();
                NotifyPropertyChanged(() => LayerList);
                
                // 初始化空的字段列表
                _fieldList = new ObservableCollection<FieldInfo>();
                NotifyPropertyChanged(() => FieldList);
                
                // 初始化空的分组字段列表
                _groupFieldList = new ObservableCollection<object>();
                NotifyPropertyChanged(() => GroupFieldList);
                
                // 初始化空的编号字段列表
                _numberFieldList = new ObservableCollection<FieldInfo>();
                NotifyPropertyChanged(() => NumberFieldList);
                
                // 初始化有效位数列表
                _digitsList = new ObservableCollection<int> { 1, 2, 3, 4, 5 };
                NotifyPropertyChanged(() => DigitsList);
                
                // 默认选择2位数
                _selectedDigit = 2;
                NotifyPropertyChanged(() => SelectedDigit);
            });
        }

        /// <summary>
        /// 清理事件订阅
        /// </summary>
        public void Cleanup()
        {
            if (_mapSelectionChangedToken != null)
            {
                MapSelectionChangedEvent.Unsubscribe(_mapSelectionChangedToken);
                _mapSelectionChangedToken = null;
            }
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        private void CloseWindow()
        {
            PresentationServices.Windows.CloseWindow("要素顺序编号");
        }
    }
}
